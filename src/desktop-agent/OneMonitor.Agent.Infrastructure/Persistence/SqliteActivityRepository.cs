using Microsoft.Data.Sqlite;
using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Infrastructure.Persistence;

/// <summary>
/// Repositório SQLite sem ORM (schema pequeno, poucas queries). Funde um registro novo
/// com o último gravado quando ambos são idênticos e contíguos no tempo, o que agrupa
/// atividade contínua mesmo entre reaberturas do repositório.
/// </summary>
public class SqliteActivityRepository : IActivityRepository
{
    private const string SelectColumns =
        "Id, ProcessName, WindowTitle, Type, StartTime, EndTime, Justification, EventId, SyncedAt";

    private readonly string _connectionString;

    public SqliteActivityRepository(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={databasePath}";
        Initialize();
    }

    private void Initialize()
    {
        using var connection = OpenConnection();

        var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS ActivityRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProcessName TEXT NOT NULL,
                WindowTitle TEXT NOT NULL,
                Type INTEGER NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT NOT NULL,
                Justification TEXT NULL,
                EventId TEXT NULL,
                SyncedAt TEXT NULL
            );
            """;
        create.ExecuteNonQuery();

        // "CREATE TABLE IF NOT EXISTS" não adiciona colunas a uma tabela já existente
        // (ex.: um banco criado em fase anterior, antes dessas colunas existirem). Migra em caso de falta.
        if (!HasColumn(connection, "ActivityRecords", "Justification"))
        {
            var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE ActivityRecords ADD COLUMN Justification TEXT NULL";
            alter.ExecuteNonQuery();
        }

        var eventIdColumnAdded = false;
        if (!HasColumn(connection, "ActivityRecords", "EventId"))
        {
            var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE ActivityRecords ADD COLUMN EventId TEXT NULL";
            alter.ExecuteNonQuery();
            eventIdColumnAdded = true;
        }

        if (!HasColumn(connection, "ActivityRecords", "SyncedAt"))
        {
            var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE ActivityRecords ADD COLUMN SyncedAt TEXT NULL";
            alter.ExecuteNonQuery();
        }

        // Linhas gravadas antes de EventId existir não têm chave de deduplicação: preenche
        // uma vez, aqui, para que a fila de sincronização funcione também para dados antigos.
        if (eventIdColumnAdded)
        {
            BackfillMissingEventIds(connection);
        }
    }

    private static void BackfillMissingEventIds(SqliteConnection connection)
    {
        var select = connection.CreateCommand();
        select.CommandText = "SELECT Id FROM ActivityRecords WHERE EventId IS NULL OR EventId = ''";
        var idsToBackfill = new List<long>();
        using (var reader = select.ExecuteReader())
        {
            while (reader.Read())
            {
                idsToBackfill.Add(reader.GetInt64(0));
            }
        }

        foreach (var id in idsToBackfill)
        {
            var update = connection.CreateCommand();
            update.CommandText = "UPDATE ActivityRecords SET EventId = $eventId WHERE Id = $id";
            update.Parameters.AddWithValue("$eventId", Guid.NewGuid().ToString());
            update.Parameters.AddWithValue("$id", id);
            update.ExecuteNonQuery();
        }
    }

    private static bool HasColumn(SqliteConnection connection, string table, string column)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            // PRAGMA table_info: coluna 1 = nome da coluna.
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public long SaveClosedRecord(ActivityRecord record)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var last = ReadLastRecord(connection);
        var canMerge =
            last is not null &&
            last.ProcessName == record.ProcessName &&
            last.WindowTitle == record.WindowTitle &&
            last.Type == record.Type &&
            last.EndTime == record.StartTime;

        long id;
        if (canMerge)
        {
            id = last!.Id;
            var update = connection.CreateCommand();
            // Reseta SyncedAt: se a linha já tinha sido sincronizada, o EndTime estendido
            // precisa ser reenviado (o backend trata EventId como chave de upsert/dedup).
            update.CommandText = "UPDATE ActivityRecords SET EndTime = $endTime, SyncedAt = NULL WHERE Id = $id";
            update.Parameters.AddWithValue("$endTime", record.EndTime.ToString("O"));
            update.Parameters.AddWithValue("$id", id);
            update.ExecuteNonQuery();
        }
        else
        {
            var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO ActivityRecords (ProcessName, WindowTitle, Type, StartTime, EndTime, Justification, EventId)
                VALUES ($processName, $windowTitle, $type, $startTime, $endTime, $justification, $eventId)
                """;
            insert.Parameters.AddWithValue("$processName", record.ProcessName);
            insert.Parameters.AddWithValue("$windowTitle", record.WindowTitle);
            insert.Parameters.AddWithValue("$type", (int)record.Type);
            insert.Parameters.AddWithValue("$startTime", record.StartTime.ToString("O"));
            insert.Parameters.AddWithValue("$endTime", record.EndTime.ToString("O"));
            insert.Parameters.AddWithValue("$justification", (object?)record.Justification ?? DBNull.Value);
            insert.Parameters.AddWithValue("$eventId",
                string.IsNullOrEmpty(record.EventId) ? Guid.NewGuid().ToString() : record.EventId);
            insert.ExecuteNonQuery();

            var lastId = connection.CreateCommand();
            lastId.CommandText = "SELECT last_insert_rowid()";
            id = (long)lastId.ExecuteScalar()!;
        }

        transaction.Commit();
        return id;
    }

    public void SetJustification(long recordId, string justification)
    {
        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE ActivityRecords SET Justification = $justification WHERE Id = $id";
        command.Parameters.AddWithValue("$justification", justification);
        command.Parameters.AddWithValue("$id", recordId);
        command.ExecuteNonQuery();
    }

    public ActivityRecord? GetLastRecord()
    {
        using var connection = OpenConnection();
        return ReadLastRecord(connection);
    }

    public IReadOnlyList<ActivityRecord> GetAllRecords()
    {
        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {SelectColumns}
            FROM ActivityRecords
            ORDER BY Id ASC
            """;
        return ReadAll(command);
    }

    public IReadOnlyList<ActivityRecord> GetRecordsForDate(DateOnly date)
    {
        // "date" é um dia do calendário local do usuário (ex.: vindo de DateTime.Now),
        // não um dia UTC. Convertemos a meia-noite local para o UTC equivalente antes de
        // comparar com StartTime/EndTime (sempre gravados em UTC) — senão, perto da
        // virada do dia, o relatório "de hoje" pode incluir/excluir horas erradas em
        // fusos diferentes de UTC+0 (ex.: Brasil, UTC-3).
        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var start = new DateTimeOffset(localMidnight).ToUniversalTime();
        var end = start.AddDays(1);

        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {SelectColumns}
            FROM ActivityRecords
            WHERE StartTime >= $start AND StartTime < $end
            ORDER BY Id ASC
            """;
        command.Parameters.AddWithValue("$start", start.ToString("O"));
        command.Parameters.AddWithValue("$end", end.ToString("O"));
        return ReadAll(command);
    }

    public IReadOnlyList<ActivityRecord> GetPendingSyncRecords(int maxCount)
    {
        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {SelectColumns}
            FROM ActivityRecords
            WHERE SyncedAt IS NULL
            ORDER BY Id ASC
            LIMIT $maxCount
            """;
        command.Parameters.AddWithValue("$maxCount", maxCount);
        return ReadAll(command);
    }

    public void MarkSynced(IReadOnlyList<long> recordIds, DateTimeOffset syncedAt)
    {
        if (recordIds.Count == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        var placeholders = string.Join(",", recordIds.Select((_, i) => $"$id{i}"));
        command.CommandText = $"UPDATE ActivityRecords SET SyncedAt = $syncedAt WHERE Id IN ({placeholders})";
        command.Parameters.AddWithValue("$syncedAt", syncedAt.ToString("O"));
        for (var i = 0; i < recordIds.Count; i++)
        {
            command.Parameters.AddWithValue($"$id{i}", recordIds[i]);
        }

        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static ActivityRecord? ReadLastRecord(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {SelectColumns}
            FROM ActivityRecords
            ORDER BY Id DESC
            LIMIT 1
            """;
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRecord(reader) : null;
    }

    private static List<ActivityRecord> ReadAll(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var results = new List<ActivityRecord>();
        while (reader.Read())
        {
            results.Add(ReadRecord(reader));
        }

        return results;
    }

    private static ActivityRecord ReadRecord(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        ProcessName = reader.GetString(1),
        WindowTitle = reader.GetString(2),
        Type = (ActivityType)reader.GetInt32(3),
        StartTime = DateTimeOffset.Parse(reader.GetString(4)),
        EndTime = DateTimeOffset.Parse(reader.GetString(5)),
        Justification = reader.IsDBNull(6) ? null : reader.GetString(6),
        EventId = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
        SyncedAt = reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8))
    };
}
