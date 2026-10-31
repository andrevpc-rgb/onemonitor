using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Tests.Fakes;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class ActivityTrackerManualPauseTests
{
    [Fact]
    public void BeginManualPause_ClosesOpenRecordAndOpensJustifiedIdleRecord()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("excel", "Planilha1 - Excel") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.Tick(); // abre registro Active
        clock.Advance(TimeSpan.FromMinutes(3));

        tracker.BeginManualPause("Almoço");

        var closed = repository.GetAllRecords().Single();
        Assert.Equal(ActivityType.Active, closed.Type);
        Assert.Equal(TimeSpan.FromMinutes(3), closed.Duration);

        Assert.NotNull(tracker.CurrentRecord);
        Assert.Equal(ActivityType.Idle, tracker.CurrentRecord!.Type);
        Assert.Equal(ActivityTracker.ManualPauseProcessName, tracker.CurrentRecord.ProcessName);
        Assert.Equal("Almoço", tracker.CurrentRecord.Justification);
    }

    [Fact]
    public void BeginManualPause_WithoutReason_StillOpensIdleRecord()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = null };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.BeginManualPause(null);

        Assert.Equal(ActivityType.Idle, tracker.CurrentRecord!.Type);
        Assert.Null(tracker.CurrentRecord.Justification);
    }

    [Fact]
    public void ResumingFromManualPause_PersistsTheJustificationOnTheClosedRecord()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = null };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        tracker.BeginManualPause("Assunto Pessoal");
        clock.Advance(TimeSpan.FromMinutes(15));

        windowProvider.Snapshot = new ActiveWindowSnapshot("outlook", "Caixa de entrada");
        tracker.Tick(); // volta a ficar Active -> fecha o registro de pausa

        var pauseRecord = repository.GetAllRecords().Single(r => r.ProcessName == ActivityTracker.ManualPauseProcessName);
        Assert.Equal("Assunto Pessoal", pauseRecord.Justification);
        Assert.Equal(TimeSpan.FromMinutes(15), pauseRecord.Duration);
    }

    [Fact]
    public void ResumingFromManualPause_DoesNotRaiseIdleReturned()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = null };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        var raisedCount = 0;
        tracker.IdleReturned += (_, _) => raisedCount++;

        tracker.BeginManualPause("Almoço");
        clock.Advance(TimeSpan.FromMinutes(20));
        windowProvider.Snapshot = new ActiveWindowSnapshot("outlook", "Caixa de entrada");
        tracker.Tick();

        Assert.Equal(0, raisedCount);
    }

    [Fact]
    public void RegularIdleReturn_StillRaisesIdleReturned_AfterAManualPauseHappenedEarlier()
    {
        // Garante que a supressão de IdleReturned é só para o registro já justificado da
        // pausa manual, não um efeito colateral que silencie o fluxo normal depois dele.
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("outlook", "Caixa de entrada") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        var raisedCount = 0;
        tracker.IdleReturned += (_, _) => raisedCount++;

        tracker.BeginManualPause("Almoço");
        clock.Advance(TimeSpan.FromMinutes(10));
        tracker.Tick(); // retoma do almoço (sem disparar)

        clock.Advance(TimeSpan.FromMinutes(1));
        idleProvider.IdleTime = TimeSpan.FromMinutes(6);
        tracker.Tick(); // agora fica ocioso de verdade (inatividade do SO)
        clock.Advance(TimeSpan.FromMinutes(8));
        idleProvider.IdleTime = TimeSpan.Zero;
        tracker.Tick(); // volta a ficar ativo -> inatividade não justificada, deve perguntar

        Assert.Equal(1, raisedCount);
    }
}
