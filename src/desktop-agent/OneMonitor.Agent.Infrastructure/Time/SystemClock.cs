using OneMonitor.Agent.Core.Abstractions;

namespace OneMonitor.Agent.Infrastructure.Time;

public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
