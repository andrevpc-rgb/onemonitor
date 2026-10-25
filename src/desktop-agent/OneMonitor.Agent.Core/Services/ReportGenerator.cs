using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Services;

/// <summary>
/// Agrega registros de atividade em um relatório consolidado. Não faz nenhuma I/O.
/// </summary>
public class ReportGenerator
{
    private readonly ICategoryResolver _categoryResolver;

    public ReportGenerator(ICategoryResolver categoryResolver)
    {
        _categoryResolver = categoryResolver;
    }

    public ReportData Generate(IReadOnlyList<ActivityRecord> records, DateOnly date)
    {
        var active = records.Where(r => r.Type == ActivityType.Active).ToList();
        var idle = records.Where(r => r.Type == ActivityType.Idle).ToList();
        var unclassified = records.Where(r => r.Type == ActivityType.Unclassified).ToList();
        var justified = idle.Where(r => !string.IsNullOrWhiteSpace(r.Justification)).ToList();

        var byApp = active
            .GroupBy(r => r.ProcessName)
            .Select(g => new AppUsage(g.Key, SumDuration(g)))
            .OrderByDescending(a => a.Duration)
            .ToList();

        var byWindow = active
            .Where(r => !string.IsNullOrWhiteSpace(r.WindowTitle))
            .GroupBy(r => (r.ProcessName, r.WindowTitle))
            .Select(g => new WindowUsage(g.Key.ProcessName, g.Key.WindowTitle, SumDuration(g)))
            .OrderByDescending(w => w.Duration)
            .ToList();

        var byCategory = active
            .GroupBy(r => _categoryResolver.Resolve(r.ProcessName, r.WindowTitle))
            .Select(g => new CategoryUsage(g.Key, SumDuration(g)))
            .OrderByDescending(c => c.Duration)
            .ToList();

        var byJustification = justified
            .GroupBy(r => r.Justification!)
            .Select(g => new JustifiedAbsence(g.Key, SumDuration(g), g.Count()))
            .OrderByDescending(j => j.Duration)
            .ToList();

        var timeline = records.OrderBy(r => r.StartTime).ToList();

        return new ReportData(
            date,
            byApp,
            byWindow,
            byCategory,
            byJustification,
            timeline,
            SumDuration(active),
            SumDuration(idle),
            SumDuration(justified),
            SumDuration(unclassified));
    }

    private static TimeSpan SumDuration(IEnumerable<ActivityRecord> records) =>
        TimeSpan.FromTicks(records.Sum(r => r.Duration.Ticks));
}
