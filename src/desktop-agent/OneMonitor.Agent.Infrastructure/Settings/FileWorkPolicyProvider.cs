using System.Text.Json;
using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Infrastructure.Settings;

/// <summary>
/// Guarda a política de expediente/pop-up em memória (como <see cref="OneMonitor.Agent.Core.Services.StaticWorkPolicyProvider"/>)
/// e também em um arquivo JSON local, para sobreviver a um reinício do agente sem precisar
/// esperar o próximo heartbeat bem-sucedido — importante porque, sem rede, o agente deve
/// continuar aplicando a última política conhecida, não voltar ao <see cref="OrganizationSettings.Default"/>.
///
/// Leitura/escrita são melhor esforço: um arquivo corrompido ou um disco momentaneamente
/// indisponível não deve impedir o agente de funcionar, só de persistir a política.
/// </summary>
public sealed class FileWorkPolicyProvider : IWorkPolicyProvider
{
    private readonly string _filePath;
    private OrganizationSettings _current;

    public FileWorkPolicyProvider(string filePath, OrganizationSettings fallback)
    {
        _filePath = filePath;
        _current = TryLoad(filePath) ?? fallback;
    }

    public OrganizationSettings Current => _current;

    public void Update(OrganizationSettings settings)
    {
        _current = settings;
        TrySave(_filePath, settings);
    }

    private static OrganizationSettings? TryLoad(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<OrganizationSettings>(File.ReadAllText(filePath));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TrySave(string filePath, OrganizationSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, JsonSerializer.Serialize(settings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Melhor esforço — ver comentário na classe.
        }
    }
}
