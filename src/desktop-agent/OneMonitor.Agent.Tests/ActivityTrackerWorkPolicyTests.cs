using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Tests.Fakes;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class ActivityTrackerWorkPolicyTests
{
    // 08:00-18:00 — mesmos valores de OrganizationSettings.Default.
    private static readonly OrganizationSettings WorkHours =
        new(new TimeOnly(8, 0), new TimeOnly(18, 0), EnableIdlePopup: true, PopupIdleThresholdMinutes: 15);

    [Fact]
    public void Tick_WhenIdleCrossesWorkEndTime_SplitsTheRecordExactlyAtTheBoundary()
    {
        // 14/01/2026, 17:50 local — dentro do expediente, 10 min antes do fim.
        var start = LocalDateTimeOffset(2026, 1, 14, 17, 50, 0);
        var clock = new ManualClock(start);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = null };
        var idleProvider = new FakeIdleTimeProvider { IdleTime = TimeSpan.FromMinutes(6) };
        var repository = new InMemoryActivityRepository();
        var workPolicy = new StaticWorkPolicyProvider(WorkHours);
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5), workPolicy);

        var raisedCount = 0;
        tracker.IdleReturned += (_, _) => raisedCount++;

        tracker.Tick(); // abre o registro Idle às 17:50.

        clock.Advance(TimeSpan.FromMinutes(20)); // agora são 18:10 — passou do fim do expediente.
        tracker.Tick(); // ainda idle (nenhuma janela ativa): deve cortar em 18:00, não disparar o pop-up.

        Assert.Equal(0, raisedCount); // usuário continua ausente — não é um "retorno" de verdade.

        var closed = Assert.Single(repository.GetAllRecords());
        Assert.Equal(ActivityType.Idle, closed.Type);
        Assert.Equal(start, closed.StartTime);
        Assert.Equal(LocalDateTimeOffset(2026, 1, 14, 18, 0, 0), closed.EndTime);
        Assert.Equal(TimeSpan.FromMinutes(10), closed.Duration);

        // O registro aberto que sucede o corte continua Idle, a partir exatamente do limite.
        Assert.NotNull(tracker.CurrentRecord);
        Assert.Equal(ActivityType.Idle, tracker.CurrentRecord!.Type);
        Assert.Equal(LocalDateTimeOffset(2026, 1, 14, 18, 0, 0), tracker.CurrentRecord.StartTime);
    }

    [Fact]
    public void Tick_WhenIdleStartsAfterWorkEndTime_NeverSplits()
    {
        // 19:00 local, já fora do expediente — nada para cortar.
        var start = LocalDateTimeOffset(2026, 1, 14, 19, 0, 0);
        var clock = new ManualClock(start);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = null };
        var idleProvider = new FakeIdleTimeProvider { IdleTime = TimeSpan.FromMinutes(6) };
        var repository = new InMemoryActivityRepository();
        var workPolicy = new StaticWorkPolicyProvider(WorkHours);
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5), workPolicy);

        tracker.Tick();
        clock.Advance(TimeSpan.FromHours(2));
        tracker.Tick();

        Assert.Empty(repository.GetAllRecords()); // nenhum corte — ainda é um único registro aberto.
        Assert.Equal(start, tracker.CurrentRecord!.StartTime);
    }

    [Fact]
    public void Tick_WhenIdleReturnedFiresAfterTheSplit_DurationStartsFromTheBoundary()
    {
        var start = LocalDateTimeOffset(2026, 1, 14, 17, 50, 0);
        var clock = new ManualClock(start);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = null };
        var idleProvider = new FakeIdleTimeProvider { IdleTime = TimeSpan.FromMinutes(6) };
        var repository = new InMemoryActivityRepository();
        var workPolicy = new StaticWorkPolicyProvider(WorkHours);
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5), workPolicy);

        IdleReturnedEventArgs? raised = null;
        tracker.IdleReturned += (_, e) => raised = e;

        tracker.Tick(); // abre Idle às 17:50.
        clock.Advance(TimeSpan.FromMinutes(20)); // 18:10 — corta em 18:00.
        tracker.Tick();

        clock.Advance(TimeSpan.FromMinutes(5)); // 18:15 — usuário finalmente volta.
        idleProvider.IdleTime = TimeSpan.Zero;
        windowProvider.Snapshot = new ActiveWindowSnapshot("devenv", "Solution - Visual Studio");
        tracker.Tick();

        Assert.NotNull(raised);
        Assert.Equal(LocalDateTimeOffset(2026, 1, 14, 18, 0, 0), raised!.IdleStart);
        Assert.Equal(TimeSpan.FromMinutes(15), raised.Duration); // 18:00 -> 18:15, não 17:50 -> 18:15.
    }

    private static DateTimeOffset LocalDateTimeOffset(int year, int month, int day, int hour, int minute, int second) =>
        new(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local));
}
