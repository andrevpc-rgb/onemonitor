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

    /// <summary>
    /// Identificador estável (GUID) gerado na criação do registro, usado como chave de
    /// deduplicação pelo backend ao sincronizar (reenvios de um lote não criam duplicatas).
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>Instante em que o registro foi confirmado como sincronizado; null = pendente.</summary>
    public DateTimeOffset? SyncedAt { get; set; }

    public TimeSpan Duration => EndTime - StartTime;
}
