namespace OneMonitor.Agent.Core.Abstractions;

/// <summary>
/// Abstrai a espera entre retentativas (backoff). Permite que testes substituam a espera
/// real por uma instantânea, sem desacelerar a suíte.
/// </summary>
public interface IAsyncDelay
{
    Task Delay(TimeSpan duration, CancellationToken cancellationToken);
}
