namespace OneMonitor.Agent.Core.Services;

/// <summary>
/// Nome amigável sugerido para o dispositivo nos lotes de sync (ver <see cref="SyncBatch"/>),
/// para o dashboard mostrar algo legível em vez do GUID gerado por
/// <see cref="OneMonitor.Agent.Infrastructure.Sync.DeviceIdentityProvider"/>.
/// </summary>
public static class DeviceNameProvider
{
    public static string GetFriendlyName() => $"{Environment.MachineName} ({Environment.UserName})";
}
