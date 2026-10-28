using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Abstractions;

/// <summary>
/// Expõe a política de expediente/pop-up vigente (ver <see cref="OrganizationSettings"/>) e
/// permite atualizá-la quando o backend devolve uma versão nova no heartbeat de sync.
/// Abstraído para o Core não depender de como ela é persistida (ver
/// Infrastructure/Settings/FileWorkPolicyProvider, que também grava em disco).
/// </summary>
public interface IWorkPolicyProvider
{
    OrganizationSettings Current { get; }

    void Update(OrganizationSettings settings);
}
