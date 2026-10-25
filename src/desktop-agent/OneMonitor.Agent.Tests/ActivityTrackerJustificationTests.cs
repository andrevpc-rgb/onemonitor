using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Tests.Fakes;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class ActivityTrackerJustificationTests
{
    [Fact]
    public void IdleReturned_FiresWhenActivityLeavesIdle()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = null };
        var idleProvider = new FakeIdleTimeProvider { IdleTime = TimeSpan.FromMinutes(6) };
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        IdleReturnedEventArgs? raised = null;
        tracker.IdleReturned += (_, e) => raised = e;

        tracker.Tick(); // abre registro Idle
        clock.Advance(TimeSpan.FromMinutes(10));

        idleProvider.IdleTime = TimeSpan.Zero;
        windowProvider.Snapshot = new ActiveWindowSnapshot("devenv", "Solution - Visual Studio");
        tracker.Tick(); // volta para Active -> deve disparar o evento

        Assert.NotNull(raised);
        Assert.Equal(TimeSpan.FromMinutes(10), raised!.Duration);
        Assert.True(raised.RecordId > 0);
    }

    [Fact]
    public void IdleReturned_DoesNotFireOnActiveToActiveTransition()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("devenv", "A") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        var raisedCount = 0;
        tracker.IdleReturned += (_, _) => raisedCount++;

        tracker.Tick();
        clock.Advance(TimeSpan.FromSeconds(5));
        windowProvider.Snapshot = new ActiveWindowSnapshot("chrome", "B");
        tracker.Tick();

        Assert.Equal(0, raisedCount);
    }

    [Fact]
    public void IdleReturned_DoesNotFireFromFlush()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = null };
        var idleProvider = new FakeIdleTimeProvider { IdleTime = TimeSpan.FromMinutes(6) };
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        var raisedCount = 0;
        tracker.IdleReturned += (_, _) => raisedCount++;

        tracker.Tick(); // abre registro Idle
        clock.Advance(TimeSpan.FromMinutes(10));
        tracker.Flush(); // pausar/encerrar enquanto ainda idle: não deve perguntar nada

        Assert.Equal(0, raisedCount);
    }

    [Fact]
    public void SetJustification_PersistsReasonOnTheIdleRecord()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = null };
        var idleProvider = new FakeIdleTimeProvider { IdleTime = TimeSpan.FromMinutes(6) };
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        long? idleRecordId = null;
        tracker.IdleReturned += (_, e) => idleRecordId = e.RecordId;

        tracker.Tick();
        clock.Advance(TimeSpan.FromMinutes(10));
        idleProvider.IdleTime = TimeSpan.Zero;
        windowProvider.Snapshot = new ActiveWindowSnapshot("devenv", "Solution - Visual Studio");
        tracker.Tick();

        Assert.NotNull(idleRecordId);
        tracker.SetJustification(idleRecordId!.Value, "Reunião Presencial");

        var idleRecord = repository.GetAllRecords().Single(r => r.Id == idleRecordId.Value);
        Assert.Equal("Reunião Presencial", idleRecord.Justification);
    }
}
