namespace OneMonitor.Agent.Core.Abstractions;

/// <summary>
/// Controla se o agente inicia junto com o Windows. Implementação real usa o Registro
/// (HKCU\...\Run); abstraído aqui para o Core não depender de Windows nem de testes tocarem
/// o Registro de verdade sem querer.
/// </summary>
public interface IAutostartManager
{
    bool IsEnabled { get; }

    void Enable();

    void Disable();

    /// <summary>Aplica o estado desejado de forma idempotente (liga se faltando, desliga se sobrando).</summary>
    void SyncTo(bool enabled)
    {
        if (enabled && !IsEnabled)
        {
            Enable();
        }
        else if (!enabled && IsEnabled)
        {
            Disable();
        }
    }
}
