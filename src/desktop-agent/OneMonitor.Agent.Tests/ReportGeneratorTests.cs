using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Tests.Fakes;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class ReportGeneratorTests
{
    [Fact]
    public void Generate_SeparatesActiveIdleAndUnclassifiedTotals()
    {
        var date = new DateOnly(2026, 1, 15);
        var baseTime = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        var records = new List<ActivityRecord>
        {
            Record("devenv", ActivityType.Active, baseTime, TimeSpan.FromMinutes(30)),
            Record("(idle)", ActivityType.Idle, baseTime.AddMinutes(30), TimeSpan.FromMinutes(10)),
            Record("chrome", ActivityType.Active, baseTime.AddMinutes(40), TimeSpan.FromMinutes(20)),
            Record("(unknown)", ActivityType.Unclassified, baseTime.AddMinutes(60), TimeSpan.FromMinutes(5))
        };

        var report = new ReportGenerator(new FakeCategoryResolver()).Generate(records, date);

        Assert.Equal(date, report.Date);
        Assert.Equal(TimeSpan.FromMinutes(50), report.TotalActive);
        Assert.Equal(TimeSpan.FromMinutes(10), report.TotalIdle);
        Assert.Equal(TimeSpan.FromMinutes(5), report.TotalUnclassified);
        Assert.Equal(TimeSpan.FromMinutes(65), report.TotalTracked);
    }

    [Fact]
    public void Generate_AggregatesActiveTimeByProcessName()
    {
        var date = new DateOnly(2026, 1, 15);
        var baseTime = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        var records = new List<ActivityRecord>
        {
            Record("chrome", ActivityType.Active, baseTime, TimeSpan.FromMinutes(10)),
            Record("devenv", ActivityType.Active, baseTime.AddMinutes(10), TimeSpan.FromMinutes(5)),
            Record("chrome", ActivityType.Active, baseTime.AddMinutes(15), TimeSpan.FromMinutes(20))
        };

        var report = new ReportGenerator(new FakeCategoryResolver()).Generate(records, date);

        Assert.Equal(2, report.AppUsages.Count);
        var chrome = Assert.Single(report.AppUsages, a => a.ProcessName == "chrome");
        Assert.Equal(TimeSpan.FromMinutes(30), chrome.Duration);
    }

    [Fact]
    public void Generate_WithNoRecords_ReturnsZeroedReport()
    {
        var report = new ReportGenerator(new FakeCategoryResolver()).Generate(new List<ActivityRecord>(), new DateOnly(2026, 1, 15));

        Assert.Empty(report.AppUsages);
        Assert.Empty(report.WindowUsages);
        Assert.Equal(TimeSpan.Zero, report.TotalTracked);
    }

    [Fact]
    public void Generate_BreaksDownActiveTimeByWindowTitleWithinSameProcess()
    {
        var date = new DateOnly(2026, 1, 15);
        var baseTime = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        var records = new List<ActivityRecord>
        {
            Record("chrome", ActivityType.Active, baseTime, TimeSpan.FromMinutes(10), "Gmail - Caixa de entrada"),
            Record("chrome", ActivityType.Active, baseTime.AddMinutes(10), TimeSpan.FromMinutes(5), "YouTube"),
            Record("chrome", ActivityType.Active, baseTime.AddMinutes(15), TimeSpan.FromMinutes(7), "Gmail - Caixa de entrada")
        };

        var report = new ReportGenerator(new FakeCategoryResolver()).Generate(records, date);

        Assert.Equal(2, report.WindowUsages.Count);
        var gmail = Assert.Single(report.WindowUsages, w => w.WindowTitle == "Gmail - Caixa de entrada");
        Assert.Equal("chrome", gmail.ProcessName);
        Assert.Equal(TimeSpan.FromMinutes(17), gmail.Duration);
        var youtube = Assert.Single(report.WindowUsages, w => w.WindowTitle == "YouTube");
        Assert.Equal(TimeSpan.FromMinutes(5), youtube.Duration);
    }

    [Fact]
    public void Generate_ExcludesRecordsWithoutWindowTitleFromWindowBreakdown()
    {
        var date = new DateOnly(2026, 1, 15);
        var baseTime = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        var records = new List<ActivityRecord>
        {
            Record("explorer", ActivityType.Active, baseTime, TimeSpan.FromMinutes(5), windowTitle: "")
        };

        var report = new ReportGenerator(new FakeCategoryResolver()).Generate(records, date);

        Assert.Single(report.AppUsages);
        Assert.Empty(report.WindowUsages);
    }

    [Fact]
    public void Generate_AggregatesActiveTimeByCategory()
    {
        var date = new DateOnly(2026, 1, 15);
        var baseTime = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
        var resolver = new FakeCategoryResolver(new Dictionary<string, string>
        {
            ["devenv"] = "Desenvolvimento",
            ["chrome"] = "Navegação"
        });

        var records = new List<ActivityRecord>
        {
            Record("devenv", ActivityType.Active, baseTime, TimeSpan.FromMinutes(20)),
            Record("chrome", ActivityType.Active, baseTime.AddMinutes(20), TimeSpan.FromMinutes(10)),
            Record("unknown-app", ActivityType.Active, baseTime.AddMinutes(30), TimeSpan.FromMinutes(5))
        };

        var report = new ReportGenerator(resolver).Generate(records, date);

        Assert.Equal(3, report.CategoryUsages.Count);
        Assert.Equal(TimeSpan.FromMinutes(20), Assert.Single(report.CategoryUsages, c => c.Category == "Desenvolvimento").Duration);
        Assert.Equal(TimeSpan.FromMinutes(10), Assert.Single(report.CategoryUsages, c => c.Category == "Navegação").Duration);
        Assert.Equal(TimeSpan.FromMinutes(5), Assert.Single(report.CategoryUsages, c => c.Category == "Outros").Duration);
    }

    [Fact]
    public void Generate_AggregatesJustifiedIdleTimeByReason_SeparatelyFromUnjustifiedIdle()
    {
        var date = new DateOnly(2026, 1, 15);
        var baseTime = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        var records = new List<ActivityRecord>
        {
            RecordWithJustification(baseTime, TimeSpan.FromMinutes(15), "Pausa"),
            RecordWithJustification(baseTime.AddMinutes(15), TimeSpan.FromMinutes(10), "Pausa"),
            RecordWithJustification(baseTime.AddMinutes(25), TimeSpan.FromMinutes(30), "Reunião Presencial"),
            Record("(idle)", ActivityType.Idle, baseTime.AddMinutes(55), TimeSpan.FromMinutes(8))
        };

        var report = new ReportGenerator(new FakeCategoryResolver()).Generate(records, date);

        Assert.Equal(2, report.JustifiedAbsences.Count);
        var pausa = Assert.Single(report.JustifiedAbsences, j => j.Justification == "Pausa");
        Assert.Equal(TimeSpan.FromMinutes(25), pausa.Duration);
        Assert.Equal(2, pausa.Occurrences);
        var reuniao = Assert.Single(report.JustifiedAbsences, j => j.Justification == "Reunião Presencial");
        Assert.Equal(TimeSpan.FromMinutes(30), reuniao.Duration);
        Assert.Equal(1, reuniao.Occurrences);

        Assert.Equal(TimeSpan.FromMinutes(55), report.TotalJustified);
        Assert.Equal(TimeSpan.FromMinutes(63), report.TotalIdle);
    }

    [Fact]
    public void Generate_IncludesAllRecordsInChronologicalOrder_RegardlessOfInputOrder()
    {
        var date = new DateOnly(2026, 1, 15);
        var baseTime = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        // Propositalmente fora de ordem, para garantir que o Generate reordena.
        var records = new List<ActivityRecord>
        {
            Record("chrome", ActivityType.Active, baseTime.AddMinutes(20), TimeSpan.FromMinutes(10)),
            Record("(idle)", ActivityType.Idle, baseTime, TimeSpan.FromMinutes(20)),
            Record("(unknown)", ActivityType.Unclassified, baseTime.AddMinutes(30), TimeSpan.FromMinutes(5))
        };

        var report = new ReportGenerator(new FakeCategoryResolver()).Generate(records, date);

        Assert.Equal(3, report.Timeline.Count);
        Assert.Equal(ActivityType.Idle, report.Timeline[0].Type);
        Assert.Equal(ActivityType.Active, report.Timeline[1].Type);
        Assert.Equal(ActivityType.Unclassified, report.Timeline[2].Type);
    }

    private static ActivityRecord Record(string processName, ActivityType type, DateTimeOffset start, TimeSpan duration, string windowTitle = "") =>
        new()
        {
            ProcessName = processName,
            WindowTitle = windowTitle,
            Type = type,
            StartTime = start,
            EndTime = start + duration
        };

    private static ActivityRecord RecordWithJustification(DateTimeOffset start, TimeSpan duration, string justification) =>
        new()
        {
            ProcessName = "(idle)",
            WindowTitle = string.Empty,
            Type = ActivityType.Idle,
            StartTime = start,
            EndTime = start + duration,
            Justification = justification
        };
}
