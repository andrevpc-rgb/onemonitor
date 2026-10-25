using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Abstractions;

public interface IActivityRepository
{
    /// <summary>
    /// Persiste um registro já fechado (com EndTime definido). Implementações podem
    /// fundir este registro com o último gravado se forem contíguos e idênticos.
    /// Retorna o Id do registro resultante (novo ou fundido), usável em <see cref="SetJustification"/>.
    /// </summary>
    long SaveClosedRecord(ActivityRecord record);

    /// <summary>Associa um motivo de ausência a um registro de inatividade já persistido.</summary>
    void SetJustification(long recordId, string justification);

    ActivityRecord? GetLastRecord();

    IReadOnlyList<ActivityRecord> GetAllRecords();

    IReadOnlyList<ActivityRecord> GetRecordsForDate(DateOnly date);
}
