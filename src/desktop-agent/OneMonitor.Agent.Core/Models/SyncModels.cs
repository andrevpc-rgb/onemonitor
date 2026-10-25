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

/// <summary>Um lote de eventos enviado em uma única requisição de sincronização.</summary>
public sealed record SyncBatch(string DeviceId, IReadOnlyList<ActivitySyncEvent> Events);
