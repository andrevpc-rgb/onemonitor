using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Tests.Fakes;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class ActivityTrackerDurationTests
{
    [Fact]
    public void Duration_AccumulatesWhileSameActivityContinues()
    {
        var start = DateTimeOffset.UtcNow;
        var clock = new ManualClock(start);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("notepad", "untitled - Notepad") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.Tick();
        clock.Advance(TimeSpan.FromSeconds(10));
        tracker.Tick();
        clock.Advance(TimeSpan.FromSeconds(15));
        tracker.Tick();

        Assert.Equal(TimeSpan.FromSeconds(25), tracker.CurrentRecord!.Duration);
    }

    [Fact]
    public void Flush_PersistsOpenRecordWithCorrectDuration()
    {
        var start = DateTimeOffset.UtcNow;
        var clock = new ManualClock(start);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("notepad", "untitled - Notepad") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.Tick();
        clock.Advance(TimeSpan.FromSeconds(30));
        tracker.Tick();
        clock.Advance(TimeSpan.FromSeconds(30));

        tracker.Flush();

        // Flush() fecha o registro no instante atual do relógio, não no último Tick --
        // por isso a duração cobre os 2 intervalos de 30s, não só o primeiro.
        var saved = Assert.Single(repository.GetAllRecords());
        Assert.Equal(TimeSpan.FromSeconds(60), saved.Duration);
        Assert.Null(tracker.CurrentRecord);
    }

    [Fact]
    public void ClosedRecord_DurationMatchesStartAndEndTime()
    {
        var start = DateTimeOffset.UtcNow;
        var clock = new ManualClock(start);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("notepad", "A - Notepad") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.Tick();
        clock.Advance(TimeSpan.FromMinutes(1));

        windowProvider.Snapshot = new ActiveWindowSnapshot("notepad", "B - Notepad");
        tracker.Tick();

        var saved = Assert.Single(repository.GetAllRecords());
        Assert.Equal(saved.EndTime - saved.StartTime, saved.Duration);
        Assert.Equal(TimeSpan.FromMinutes(1), saved.Duration);
    }
}
