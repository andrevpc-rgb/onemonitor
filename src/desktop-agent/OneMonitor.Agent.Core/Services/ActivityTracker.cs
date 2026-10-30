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
    private readonly IWorkPolicyProvider _workPolicyProvider;

    private ActivityRecord? _openRecord;

    public ActivityTracker(
        IActiveWindowProvider windowProvider,
        IIdleTimeProvider idleTimeProvider,
        IActivityRepository repository,
        IClock clock,
        TimeSpan idleThreshold,
        IWorkPolicyProvider? workPolicyProvider = null)
    {
        _windowProvider = windowProvider;
        _idleTimeProvider = idleTimeProvider;
        _repository = repository;
        _clock = clock;
        _idleThreshold = idleThreshold;
        _workPolicyProvider = workPolicyProvider ?? new StaticWorkPolicyProvider(OrganizationSettings.Unrestricted);
    }

    /// <summary>Registro em memória ainda não persistido, refletindo a atividade corrente.</summary>
    public ActivityRecord? CurrentRecord => _openRecord;

    /// <summary>
    /// Política de expediente/pop-up vigente — exposta para quem decide se mostra o pop-up
    /// (ver JustificationPolicy, chamada pela Agent) e para quem a atualiza após um sync
    /// bem-sucedido, sem precisar de uma segunda instância/injeção separada.
    /// </summary>
    public IWorkPolicyProvider WorkPolicyProvider => _workPolicyProvider;

    /// <summary>
    /// Disparado quando a atividade sai de Idle para qualquer outro tipo (usuário voltou de
    /// um período de inatividade). Não dispara a partir de <see cref="Flush"/> — pausar ou
    /// encerrar o monitoramento não deve interromper o usuário com um pedido de justificativa.
    /// </summary>
    public event EventHandler<IdleReturnedEventArgs>? IdleReturned;

    /// <summary>
    /// Disparado quando uma nova atividade Ativa é aberta (processo/janela diferente do
    /// registro anterior — ver agrupamento em <see cref="Tick"/>) e é classificada como
    /// Improdutiva pelas regras vigentes (<see cref="ProductivityClassifier"/>, com as regras
    /// da organização em <see cref="IWorkPolicyProvider.Current"/>). Dispara uma vez por
    /// transição, não a cada tick — a TrayApplicationContext decide como alertar o usuário.
    /// </summary>
    public event EventHandler<UnproductiveActivityDetectedEventArgs>? UnproductiveActivityDetected;

    public void Tick()
    {
        var now = _clock.UtcNow;

        SplitIdleRecordAtWorkEndIfNeeded(now);

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
            RaiseIfUnproductive(_openRecord);
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
            var closedRecord = _openRecord;
            var closedRecordId = _repository.SaveClosedRecord(closedRecord);
            _openRecord = CreateRecord(type, processName, windowTitle, now);
            RaiseIfUnproductive(_openRecord);

            if (closedRecord.Type == ActivityType.Idle && type != ActivityType.Idle)
            {
                IdleReturned?.Invoke(this, new IdleReturnedEventArgs(closedRecordId, closedRecord.StartTime, closedRecord.EndTime));
            }
        }
    }

    /// <summary>Associa um motivo de ausência a um período de inatividade já persistido.</summary>
    public void SetJustification(long recordId, string justification) =>
        _repository.SetJustification(recordId, justification);

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

    /// <summary>
    /// Corte de expediente: se o registro aberto é Idle e começou antes do fim do
    /// expediente (horário local) do dia dele, mas "agora" já passou desse horário, fecha-o
    /// exatamente no limite e abre um novo registro Idle a partir dali — em vez de deixá-lo
    /// se estender até o usuário realmente voltar, possivelmente no dia seguinte, e então
    /// reportar uma "ausência" de muitas horas que nunca foi tempo de expediente de fato
    /// (ver JustificationPolicy, que depende desta duração já vir correta). Não dispara
    /// IdleReturned: o usuário continua ausente, isto é só um corte administrativo de dados.
    /// </summary>
    private void SplitIdleRecordAtWorkEndIfNeeded(DateTimeOffset now)
    {
        if (_openRecord is not { Type: ActivityType.Idle } openIdleRecord)
        {
            return;
        }

        var workEndBoundary = WorkEndBoundaryUtc(openIdleRecord.StartTime);
        if (openIdleRecord.StartTime >= workEndBoundary || now < workEndBoundary)
        {
            return;
        }

        openIdleRecord.EndTime = workEndBoundary;
        _repository.SaveClosedRecord(openIdleRecord);
        _openRecord = CreateRecord(ActivityType.Idle, "(idle)", string.Empty, workEndBoundary);
    }

    /// <summary>Fim do expediente (horário local), no mesmo dia civil local de <paramref name="instant"/>.</summary>
    private DateTimeOffset WorkEndBoundaryUtc(DateTimeOffset instant)
    {
        var local = instant.ToLocalTime();
        var workEndTime = _workPolicyProvider.Current.WorkEndTime;
        var boundaryLocal = new DateTimeOffset(
            local.Year, local.Month, local.Day,
            workEndTime.Hour, workEndTime.Minute, workEndTime.Second,
            local.Offset);
        return boundaryLocal.ToUniversalTime();
    }

    private void RaiseIfUnproductive(ActivityRecord record)
    {
        if (record.Type != ActivityType.Active)
        {
            return;
        }

        var classification = ProductivityClassifier.Classify(
            record.ProcessName, record.WindowTitle, _workPolicyProvider.Current.ProductivityRules);

        if (classification == ProductivityClass.Unproductive)
        {
            UnproductiveActivityDetected?.Invoke(
                this, new UnproductiveActivityDetectedEventArgs(record.ProcessName, record.WindowTitle));
        }
    }

    private static ActivityRecord CreateRecord(ActivityType type, string processName, string windowTitle, DateTimeOffset now) =>
        new()
        {
            EventId = Guid.NewGuid().ToString(),
            Type = type,
            ProcessName = processName,
            WindowTitle = windowTitle,
            StartTime = now,
            EndTime = now
        };
}
