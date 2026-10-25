using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Services;

/// <summary>
/// Agrega registros de atividade em um relatório consolidado. Não faz nenhuma I/O.
/// </summary>
public class ReportGenerator
{
    public ReportData Generate(IReadOnlyList<ActivityRecord> records, DateOnly date)
    {
        var active = records.Where(r => r.Type == ActivityType.Active).ToList();
        var idle = records.Where(r => r.Type == ActivityType.Idle).ToList();
        var unclassified = records.Where(r => r.Type == ActivityType.Unclassified).ToList();

        var byApp = active
            .GroupBy(r => r.ProcessName)
            .Select(g => new AppUsage(g.Key, SumDuration(g)))
            .OrderByDescending(a => a.Duration)
            .ToList();

        return new ReportData(
            date,
            byApp,
            SumDuration(active),
            SumDuration(idle),
            SumDuration(unclassified));
    }

    private static TimeSpan SumDuration(IEnumerable<ActivityRecord> records) =>
        TimeSpan.FromTicks(records.Sum(r => r.Duration.Ticks));
}
