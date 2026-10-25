namespace OneMonitor.Agent.Infrastructure.Sync;

/// <summary>
/// Gera e persiste um identificador de dispositivo (GUID) estável entre reinícios do
/// agente, usado para autenticar/atribuir eventos de sincronização a esta instalação.
/// </summary>
public static class DeviceIdentityProvider
{
    public static string GetOrCreateDeviceId(string filePath)
    {
        if (File.Exists(filePath))
        {
            var existing = File.ReadAllText(filePath).Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var deviceId = Guid.NewGuid().ToString();
        File.WriteAllText(filePath, deviceId);
        return deviceId;
    }
}
