using OneMonitor.Agent.Core.Abstractions;

namespace OneMonitor.Agent.Tests.Fakes;

/// <summary>Relógio controlado manualmente nos testes, para simular a passagem do tempo.</summary>
public class ManualClock : IClock
{
    public ManualClock(DateTimeOffset start) => UtcNow = start;

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan amount) => UtcNow += amount;
}
