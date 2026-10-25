namespace OneMonitor.Agent.Core.Models;

public sealed record AppUsage(string ProcessName, TimeSpan Duration);

public sealed record ReportData(
    DateOnly Date,
    IReadOnlyList<AppUsage> AppUsages,
    TimeSpan TotalActive,
    TimeSpan TotalIdle,
    TimeSpan TotalUnclassified)
{
    public TimeSpan TotalTracked => TotalActive + TotalIdle + TotalUnclassified;
}
