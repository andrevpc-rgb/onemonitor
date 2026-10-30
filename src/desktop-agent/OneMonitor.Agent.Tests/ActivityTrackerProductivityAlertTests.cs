using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Tests.Fakes;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class ActivityTrackerProductivityAlertTests
{
    [Fact]
    public void UnproductiveActivityDetected_FiresWhenANewUnproductiveWindowOpens()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("chrome", "YouTube - Google Chrome") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        UnproductiveActivityDetectedEventArgs? raised = null;
        tracker.UnproductiveActivityDetected += (_, e) => raised = e;

        tracker.Tick();

        Assert.NotNull(raised);
        Assert.Equal("chrome", raised!.ProcessName);
        Assert.Equal("YouTube - Google Chrome", raised.WindowTitle);
    }

    [Fact]
    public void UnproductiveActivityDetected_DoesNotFire_ForProductiveOrNeutralActivity()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("EXCEL", "planilha.xlsx") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        var raisedCount = 0;
        tracker.UnproductiveActivityDetected += (_, _) => raisedCount++;

        tracker.Tick();

        Assert.Equal(0, raisedCount);
    }

    [Fact]
    public void UnproductiveActivityDetected_FiresOnlyOnceWhileTheSameWindowStaysOpen()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("chrome", "YouTube - Google Chrome") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        var raisedCount = 0;
        tracker.UnproductiveActivityDetected += (_, _) => raisedCount++;

        tracker.Tick();
        clock.Advance(TimeSpan.FromSeconds(2));
        tracker.Tick(); // mesma janela ainda aberta -> só estende o registro, não dispara de novo
        clock.Advance(TimeSpan.FromSeconds(2));
        tracker.Tick();

        Assert.Equal(1, raisedCount);
    }

    [Fact]
    public void UnproductiveActivityDetected_UsesOrganizationRules_WhenProvided()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        // "sistema-interno" não bate em nenhuma palavra-chave padrão, mas a organização marcou
        // como improdutivo nas suas próprias regras (ver organization_productivity_rules).
        var rules = new[] { new ProductivityRuleDto("sistema-interno", "unproductive") };
        var workPolicyProvider = new StaticWorkPolicyProvider(OrganizationSettings.Default with { ProductivityRules = rules });

        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("sistema-interno", "Jogo da empresa") };
        var idleProvider = new FakeIdleTimeProvider();
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5), workPolicyProvider);

        UnproductiveActivityDetectedEventArgs? raised = null;
        tracker.UnproductiveActivityDetected += (_, e) => raised = e;

        tracker.Tick();

        Assert.NotNull(raised);
        Assert.Equal("sistema-interno", raised!.ProcessName);
    }

    [Fact]
    public void UnproductiveActivityDetected_DoesNotFire_WhileIdle()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = null };
        var idleProvider = new FakeIdleTimeProvider { IdleTime = TimeSpan.FromMinutes(6) };
        var repository = new InMemoryActivityRepository();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repository, clock, TimeSpan.FromMinutes(5));

        var raisedCount = 0;
        tracker.UnproductiveActivityDetected += (_, _) => raisedCount++;

        tracker.Tick(); // abre registro Idle, nunca classificado

        Assert.Equal(0, raisedCount);
    }
}
