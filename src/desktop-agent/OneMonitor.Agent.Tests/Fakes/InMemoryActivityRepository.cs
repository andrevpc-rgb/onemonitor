using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Tests.Fakes;

/// <summary>
/// Repositório em memória que replica a regra de fusão do SqliteActivityRepository,
/// usado para testar o ActivityTracker sem depender de SQLite.
/// </summary>
public class InMemoryActivityRepository : IActivityRepository
{
    private readonly List<ActivityRecord> _records = new();

    public void SaveClosedRecord(ActivityRecord record)
    {
        var last = _records.Count > 0 ? _records[^1] : null;
        var canMerge =
            last is not null &&
            last.ProcessName == record.ProcessName &&
            last.WindowTitle == record.WindowTitle &&
            last.Type == record.Type &&
            last.EndTime == record.StartTime;

        if (canMerge)
        {
            last!.EndTime = record.EndTime;
        }
        else
        {
            _records.Add(new ActivityRecord
            {
                Id = _records.Count + 1,
                ProcessName = record.ProcessName,
                WindowTitle = record.WindowTitle,
                Type = record.Type,
                StartTime = record.StartTime,
                EndTime = record.EndTime
            });
        }
    }

    public ActivityRecord? GetLastRecord() => _records.Count > 0 ? _records[^1] : null;

    public IReadOnlyList<ActivityRecord> GetAllRecords() => _records.AsReadOnly();

    public IReadOnlyList<ActivityRecord> GetRecordsForDate(DateOnly date) =>
        _records.Where(r => DateOnly.FromDateTime(r.StartTime.UtcDateTime) == date).ToList();
}
