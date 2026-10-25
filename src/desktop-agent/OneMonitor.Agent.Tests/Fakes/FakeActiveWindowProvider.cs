using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Tests.Fakes;

public class FakeActiveWindowProvider : IActiveWindowProvider
{
    public ActiveWindowSnapshot? Snapshot { get; set; }

    public ActiveWindowSnapshot? GetActiveWindow() => Snapshot;
}
