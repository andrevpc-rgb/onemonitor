using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Abstractions;

public interface IActivityRepository
{
    /// <summary>
    /// Persiste um registro já fechado (com EndTime definido). Implementações podem
    /// fundir este registro com o último gravado se forem contíguos e idênticos.
    /// </summary>
    void SaveClosedRecord(ActivityRecord record);

    ActivityRecord? GetLastRecord();

    IReadOnlyList<ActivityRecord> GetAllRecords();

    IReadOnlyList<ActivityRecord> GetRecordsForDate(DateOnly date);
}
