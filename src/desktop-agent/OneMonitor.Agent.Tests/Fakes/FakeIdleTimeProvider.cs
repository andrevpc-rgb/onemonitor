using OneMonitor.Agent.Core.Abstractions;

namespace OneMonitor.Agent.Tests.Fakes;

public class FakeIdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan IdleTime { get; set; } = TimeSpan.Zero;

    public TimeSpan GetIdleTime() => IdleTime;
}
