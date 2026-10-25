using OneMonitor.Agent.Core.Abstractions;

namespace OneMonitor.Agent.Core.Services;

/// <summary>Implementação real de <see cref="IAsyncDelay"/> via Task.Delay (sem dependência de plataforma).</summary>
public class TaskDelay : IAsyncDelay
{
    public Task Delay(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration, cancellationToken);
}
