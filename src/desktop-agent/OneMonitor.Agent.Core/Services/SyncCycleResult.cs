namespace OneMonitor.Agent.Core.Services;

/// <summary>Resultado de um ciclo de sincronização (um lote, no máximo).</summary>
public sealed record SyncCycleResult(bool Success, int EventsSynced, string? Error)
{
    public static SyncCycleResult Synced(int count) => new(true, count, null);

    public static SyncCycleResult Failed(string? error) => new(false, 0, error);
}
