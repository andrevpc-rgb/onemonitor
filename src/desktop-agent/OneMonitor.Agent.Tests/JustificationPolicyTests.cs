using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class JustificationPolicyTests
{
    // 08:00-18:00, pop-up a partir de 15 min — os mesmos valores de OrganizationSettings.Default.
    private static readonly OrganizationSettings Settings =
        new(new TimeOnly(8, 0), new TimeOnly(18, 0), EnableIdlePopup: true, PopupIdleThresholdMinutes: 15);

    [Fact]
    public void ShouldPrompt_WithinWorkHoursOnAWeekday_AboveThreshold_ReturnsTrue()
    {
        // 14/01/2026 é uma quarta-feira.
        var idleStart = LocalDateTimeOffset(2026, 1, 14, 10, 0, 0);
        var e = new IdleReturnedEventArgs(1, idleStart, idleStart.AddMinutes(20));

        Assert.True(JustificationPolicy.ShouldPrompt(e, Settings));
    }

    [Fact]
    public void ShouldPrompt_WhenPopupDisabled_ReturnsFalse()
    {
        var disabled = Settings with { EnableIdlePopup = false };
        var idleStart = LocalDateTimeOffset(2026, 1, 14, 10, 0, 0);
        var e = new IdleReturnedEventArgs(1, idleStart, idleStart.AddMinutes(20));

        Assert.False(JustificationPolicy.ShouldPrompt(e, disabled));
    }

    [Fact]
    public void ShouldPrompt_WhenDurationBelowThreshold_ReturnsFalse()
    {
        var idleStart = LocalDateTimeOffset(2026, 1, 14, 10, 0, 0);
        var e = new IdleReturnedEventArgs(1, idleStart, idleStart.AddMinutes(5)); // < 15 min

        Assert.False(JustificationPolicy.ShouldPrompt(e, Settings));
    }

    [Fact]
    public void ShouldPrompt_WhenIdleStartedOnASaturday_ReturnsFalse()
    {
        // 17/01/2026 é um sábado.
        var idleStart = LocalDateTimeOffset(2026, 1, 17, 10, 0, 0);
        var e = new IdleReturnedEventArgs(1, idleStart, idleStart.AddMinutes(20));

        Assert.False(JustificationPolicy.ShouldPrompt(e, Settings));
    }

    [Fact]
    public void ShouldPrompt_WhenIdleStartedOnASunday_ReturnsFalse()
    {
        // 18/01/2026 é um domingo.
        var idleStart = LocalDateTimeOffset(2026, 1, 18, 10, 0, 0);
        var e = new IdleReturnedEventArgs(1, idleStart, idleStart.AddMinutes(20));

        Assert.False(JustificationPolicy.ShouldPrompt(e, Settings));
    }

    [Fact]
    public void ShouldPrompt_WhenIdleStartedBeforeWorkStartTime_ReturnsFalse()
    {
        var idleStart = LocalDateTimeOffset(2026, 1, 14, 6, 0, 0); // antes das 08:00
        var e = new IdleReturnedEventArgs(1, idleStart, idleStart.AddMinutes(20));

        Assert.False(JustificationPolicy.ShouldPrompt(e, Settings));
    }

    [Fact]
    public void ShouldPrompt_WhenIdleStartedAtOrAfterWorkEndTime_ReturnsFalse()
    {
        // Este é exatamente o caso que ActivityTracker.SplitIdleRecordAtWorkEndIfNeeded
        // produz: o bloco "depois do expediente" sempre começa EXATAMENTE no fim do
        // expediente (18:00) — a trava precisa tratar isso como fora da janela.
        var idleStart = LocalDateTimeOffset(2026, 1, 14, 18, 0, 0);
        var e = new IdleReturnedEventArgs(1, idleStart, idleStart.AddHours(15));

        Assert.False(JustificationPolicy.ShouldPrompt(e, Settings));
    }

    [Fact]
    public void ShouldPrompt_WithUnrestrictedSettings_IgnoresWorkHoursWindow()
    {
        // Unrestricted (00:00-23:59:59) remove a trava de horário, mas o fim de semana é
        // uma regra fixa do JustificationPolicy, não um campo de OrganizationSettings —
        // continua valendo mesmo aqui. Por isso o teste usa uma quarta-feira de madrugada.
        var idleStart = LocalDateTimeOffset(2026, 1, 14, 2, 0, 0);
        var e = new IdleReturnedEventArgs(1, idleStart, idleStart.AddMinutes(20));

        Assert.True(JustificationPolicy.ShouldPrompt(e, OrganizationSettings.Unrestricted));
    }

    [Fact]
    public void ShouldPrompt_WithUnrestrictedSettings_StillBlocksOnAWeekend()
    {
        var idleStart = LocalDateTimeOffset(2026, 1, 17, 2, 0, 0); // sábado de madrugada
        var e = new IdleReturnedEventArgs(1, idleStart, idleStart.AddMinutes(20));

        Assert.False(JustificationPolicy.ShouldPrompt(e, OrganizationSettings.Unrestricted));
    }

    // DateTimeOffset(DateTime) com Kind=Local usa o fuso da própria máquina — faz
    // ToLocalTime() devolver exatamente o horário de parede pedido, em qualquer máquina.
    private static DateTimeOffset LocalDateTimeOffset(int year, int month, int day, int hour, int minute, int second) =>
        new(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local));
}
