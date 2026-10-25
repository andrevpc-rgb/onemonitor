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

    public long SaveClosedRecord(ActivityRecord record)
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
            last.SyncedAt = null; // mesma regra do SqliteActivityRepository: fusão reabre pendência de sync.
            return last.Id;
        }

        var newRecord = new ActivityRecord
        {
            Id = _records.Count + 1,
            ProcessName = record.ProcessName,
            WindowTitle = record.WindowTitle,
            Type = record.Type,
            StartTime = record.StartTime,
            EndTime = record.EndTime,
            Justification = record.Justification,
            EventId = string.IsNullOrEmpty(record.EventId) ? Guid.NewGuid().ToString() : record.EventId
        };
        _records.Add(newRecord);
        return newRecord.Id;
    }

    public void SetJustification(long recordId, string justification)
    {
        var record = _records.FirstOrDefault(r => r.Id == recordId);
        if (record is not null)
        {
            record.Justification = justification;
        }
    }

    public ActivityRecord? GetLastRecord() => _records.Count > 0 ? _records[^1] : null;

    public IReadOnlyList<ActivityRecord> GetAllRecords() => _records.AsReadOnly();

    public IReadOnlyList<ActivityRecord> GetRecordsForDate(DateOnly date) =>
        _records.Where(r => DateOnly.FromDateTime(r.StartTime.UtcDateTime) == date).ToList();

    public IReadOnlyList<ActivityRecord> GetPendingSyncRecords(int maxCount) =>
        _records.Where(r => r.SyncedAt is null).Take(maxCount).ToList();

    public void MarkSynced(IReadOnlyList<long> recordIds, DateTimeOffset syncedAt)
    {
        foreach (var id in recordIds)
        {
            var record = _records.FirstOrDefault(r => r.Id == id);
            if (record is not null)
            {
                record.SyncedAt = syncedAt;
            }
        }
    }
}
