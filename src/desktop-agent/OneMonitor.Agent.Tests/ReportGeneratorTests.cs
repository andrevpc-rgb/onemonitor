using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
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

        var report = new ReportGenerator().Generate(records, date);

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

        var report = new ReportGenerator().Generate(records, date);

        Assert.Equal(2, report.AppUsages.Count);
        var chrome = Assert.Single(report.AppUsages, a => a.ProcessName == "chrome");
        Assert.Equal(TimeSpan.FromMinutes(30), chrome.Duration);
    }

    [Fact]
    public void Generate_WithNoRecords_ReturnsZeroedReport()
    {
        var report = new ReportGenerator().Generate(new List<ActivityRecord>(), new DateOnly(2026, 1, 15));

        Assert.Empty(report.AppUsages);
        Assert.Equal(TimeSpan.Zero, report.TotalTracked);
    }

    private static ActivityRecord Record(string processName, ActivityType type, DateTimeOffset start, TimeSpan duration) =>
        new()
        {
            ProcessName = processName,
            WindowTitle = string.Empty,
            Type = type,
            StartTime = start,
            EndTime = start + duration
        };
}
