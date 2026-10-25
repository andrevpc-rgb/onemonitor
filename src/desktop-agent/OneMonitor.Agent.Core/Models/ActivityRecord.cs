namespace OneMonitor.Agent.Core.Models;

public class ActivityRecord
{
    public long Id { get; set; }

    public string ProcessName { get; set; } = string.Empty;

    public string WindowTitle { get; set; } = string.Empty;

    public ActivityType Type { get; set; }

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; set; }

    /// <summary>Motivo informado pelo usuário para um período de inatividade (null = não justificado).</summary>
    public string? Justification { get; set; }

    public TimeSpan Duration => EndTime - StartTime;
}
