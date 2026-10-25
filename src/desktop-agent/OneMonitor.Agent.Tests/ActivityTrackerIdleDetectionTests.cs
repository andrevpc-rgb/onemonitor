using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Tests.Fakes;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class ActivityTrackerIdleDetectionTests
{
    [Fact]
    public void IdleTimeBelowThreshold_IsClassifiedAsActive()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("excel", "Planilha1 - Excel") };
        var idleProvider = new FakeIdleTimeProvider { IdleTime = TimeSpan.FromMinutes(4) };
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.Tick();

        Assert.Equal(ActivityType.Active, tracker.CurrentRecord!.Type);
    }

    [Fact]
    public void IdleTimeAtOrAboveThreshold_IsClassifiedAsIdle()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("excel", "Planilha1 - Excel") };
        var idleProvider = new FakeIdleTimeProvider { IdleTime = TimeSpan.FromMinutes(5) };
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.Tick();

        Assert.Equal(ActivityType.Idle, tracker.CurrentRecord!.Type);
    }

    [Fact]
    public void TransitionFromActiveToIdle_ClosesActiveRecordAndOpensIdleRecord()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("excel", "Planilha1 - Excel") };
        var idleProvider = new FakeIdleTimeProvider { IdleTime = TimeSpan.Zero };
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.Tick();
        clock.Advance(TimeSpan.FromMinutes(1));

        idleProvider.IdleTime = TimeSpan.FromMinutes(6);
        tracker.Tick();

        var saved = Assert.Single(repository.GetAllRecords());
        Assert.Equal(ActivityType.Active, saved.Type);
        Assert.Equal(ActivityType.Idle, tracker.CurrentRecord!.Type);
    }

    [Fact]
    public void NoActiveWindowAvailable_IsClassifiedAsUnclassified()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = null };
        var idleProvider = new FakeIdleTimeProvider { IdleTime = TimeSpan.Zero };
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.Tick();

        Assert.Equal(ActivityType.Unclassified, tracker.CurrentRecord!.Type);
    }
}
