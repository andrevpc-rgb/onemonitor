using OneMonitor.Agent.Core.Abstractions;

namespace OneMonitor.Agent.Tests.Fakes;

/// <summary>Não espera de verdade — só registra os atrasos pedidos, para os testes ficarem rápidos.</summary>
public class FakeAsyncDelay : IAsyncDelay
{
    public List<TimeSpan> RequestedDelays { get; } = new();

    public Task Delay(TimeSpan duration, CancellationToken cancellationToken)
    {
        RequestedDelays.Add(duration);
        return Task.CompletedTask;
    }
}
