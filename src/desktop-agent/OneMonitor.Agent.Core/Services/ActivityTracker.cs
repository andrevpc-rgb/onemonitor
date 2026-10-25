using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Services;

/// <summary>
/// Máquina de estados do monitoramento: a cada <see cref="Tick"/>, classifica o instante
/// atual (Active/Idle/Unclassified) e mantém um único registro "aberto" em memória,
/// estendendo-o enquanto a atividade não mudar (agrupamento) e persistindo-o quando mudar.
/// </summary>
public class ActivityTracker
{
    private readonly IActiveWindowProvider _windowProvider;
    private readonly IIdleTimeProvider _idleTimeProvider;
    private readonly IActivityRepository _repository;
    private readonly IClock _clock;
    private readonly TimeSpan _idleThreshold;

    private ActivityRecord? _openRecord;

    public ActivityTracker(
        IActiveWindowProvider windowProvider,
        IIdleTimeProvider idleTimeProvider,
        IActivityRepository repository,
        IClock clock,
        TimeSpan idleThreshold)
    {
        _windowProvider = windowProvider;
        _idleTimeProvider = idleTimeProvider;
        _repository = repository;
        _clock = clock;
        _idleThreshold = idleThreshold;
    }

    /// <summary>Registro em memória ainda não persistido, refletindo a atividade corrente.</summary>
    public ActivityRecord? CurrentRecord => _openRecord;

    public void Tick()
    {
        var now = _clock.UtcNow;
        var idleTime = _idleTimeProvider.GetIdleTime();

        ActivityType type;
        string processName;
        string windowTitle;

        if (idleTime >= _idleThreshold)
        {
            type = ActivityType.Idle;
            processName = "(idle)";
            windowTitle = string.Empty;
        }
        else
        {
            var snapshot = _windowProvider.GetActiveWindow();
            if (snapshot is null)
            {
                type = ActivityType.Unclassified;
                processName = "(unknown)";
                windowTitle = string.Empty;
            }
            else
            {
                type = ActivityType.Active;
                processName = snapshot.ProcessName;
                windowTitle = snapshot.WindowTitle;
            }
        }

        if (_openRecord is null)
        {
            _openRecord = CreateRecord(type, processName, windowTitle, now);
            return;
        }

        var sameActivity =
            _openRecord.Type == type &&
            _openRecord.ProcessName == processName &&
            _openRecord.WindowTitle == windowTitle;

        if (sameActivity)
        {
            _openRecord.EndTime = now;
        }
        else
        {
            // O registro antigo termina agora, no instante em que a mudança foi detectada
            // (e não no último tick em que a atividade ainda era a mesma).
            _openRecord.EndTime = now;
            _repository.SaveClosedRecord(_openRecord);
            _openRecord = CreateRecord(type, processName, windowTitle, now);
        }
    }

    /// <summary>Fecha e persiste o registro aberto, se houver (usado ao pausar/encerrar).</summary>
    public void Flush()
    {
        if (_openRecord is null)
        {
            return;
        }

        _openRecord.EndTime = _clock.UtcNow;
        _repository.SaveClosedRecord(_openRecord);
        _openRecord = null;
    }

    private static ActivityRecord CreateRecord(ActivityType type, string processName, string windowTitle, DateTimeOffset now) =>
        new()
        {
            Type = type,
            ProcessName = processName,
            WindowTitle = windowTitle,
            StartTime = now,
            EndTime = now
        };
}
