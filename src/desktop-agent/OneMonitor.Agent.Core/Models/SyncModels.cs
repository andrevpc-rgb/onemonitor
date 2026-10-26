namespace OneMonitor.Agent.Core.Models;

/// <summary>
/// Contrato de um evento de atividade (ou período de inatividade, justificado ou não)
/// enviado ao backend. Campos em texto (Type) em vez do enum .NET para manter o contrato
/// estável independente de como o enum evoluir no agente.
/// </summary>
public sealed record ActivitySyncEvent(
    string EventId,
    string DeviceId,
    string ProcessName,
    string WindowTitle,
    string Type,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Justification,
    string Category);

/// <summary>
/// Sinal de vida do agente. "Started" (uma vez, ao iniciar) e "Stopped" (ao encerrar pela
/// bandeja) são explícitos; "Heartbeat" acompanha todo ciclo periódico de sincronização,
/// mesmo quando não há atividade pendente — é isso que deixa o backend saber que o agente
/// continua rodando, não só que ele tem dados novos.
/// </summary>
public sealed record DeviceStatusEvent(string State, DateTimeOffset Timestamp)
{
    public const string Started = "Started";
    public const string Heartbeat = "Heartbeat";
    public const string Stopped = "Stopped";
}

/// <summary>Um lote de eventos enviado em uma única requisição de sincronização.</summary>
public sealed record SyncBatch(string DeviceId, IReadOnlyList<ActivitySyncEvent> Events, DeviceStatusEvent? Status = null);
