namespace OneMonitor.Agent.Core.Abstractions;

/// <summary>
/// Tempo decorrido desde o último input de teclado/mouse.
/// Implementações devem expor apenas o timestamp, nunca a tecla/botão.
/// </summary>
public interface IIdleTimeProvider
{
    TimeSpan GetIdleTime();
}
