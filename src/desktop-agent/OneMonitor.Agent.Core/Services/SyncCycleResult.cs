using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Services;

/// <summary>Resultado de um ciclo de sincronização (um lote, no máximo).</summary>
public sealed record SyncCycleResult(bool Success, int EventsSynced, string? Error, OrganizationSettings? Settings = null)
{
    public static SyncCycleResult Synced(int count, OrganizationSettings? settings = null) => new(true, count, null, settings);

    public static SyncCycleResult Failed(string? error) => new(false, 0, error);
}
