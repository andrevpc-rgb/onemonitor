using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Tests.Fakes;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class ActivityTrackerGroupingTests
{
    [Fact]
    public void ConsecutiveTicksOfSameWindow_AreGroupedIntoOneRecord()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("devenv", "Solution - Visual Studio") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.Tick();
        clock.Advance(TimeSpan.FromSeconds(2));
        tracker.Tick();
        clock.Advance(TimeSpan.FromSeconds(2));
        tracker.Tick();

        Assert.Empty(repository.GetAllRecords());
        Assert.NotNull(tracker.CurrentRecord);
        Assert.Equal("devenv", tracker.CurrentRecord!.ProcessName);
    }

    [Fact]
    public void WindowChange_ClosesPreviousRecordAndStartsNewOne()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("devenv", "Solution - Visual Studio") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.Tick();
        clock.Advance(TimeSpan.FromSeconds(2));
        tracker.Tick();

        windowProvider.Snapshot = new ActiveWindowSnapshot("chrome", "OneMonitor - Google Chrome");
        clock.Advance(TimeSpan.FromSeconds(2));
        tracker.Tick();

        var saved = repository.GetAllRecords();
        Assert.Single(saved);
        Assert.Equal("devenv", saved[0].ProcessName);
        Assert.Equal("chrome", tracker.CurrentRecord!.ProcessName);
    }

    [Fact]
    public void TitleChange_OnSameProcess_StartsNewRecord()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("chrome", "Tab A - Google Chrome") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.Tick();
        clock.Advance(TimeSpan.FromSeconds(2));

        windowProvider.Snapshot = new ActiveWindowSnapshot("chrome", "Tab B - Google Chrome");
        tracker.Tick();

        var saved = repository.GetAllRecords();
        Assert.Single(saved);
        Assert.Equal("Tab A - Google Chrome", saved[0].WindowTitle);
    }
}
