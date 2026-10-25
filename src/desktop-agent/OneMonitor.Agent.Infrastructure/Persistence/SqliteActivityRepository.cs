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
                Justification TEXT NULL
            );
            """;
        create.ExecuteNonQuery();

        // "CREATE TABLE IF NOT EXISTS" não adiciona colunas a uma tabela já existente
        // (ex.: um banco criado na Fase 1, antes de Justification existir). Migra em caso de falta.
        if (!HasColumn(connection, "ActivityRecords", "Justification"))
        {
            var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE ActivityRecords ADD COLUMN Justification TEXT NULL";
            alter.ExecuteNonQuery();
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
            update.CommandText = "UPDATE ActivityRecords SET EndTime = $endTime WHERE Id = $id";
            update.Parameters.AddWithValue("$endTime", record.EndTime.ToString("O"));
            update.Parameters.AddWithValue("$id", id);
            update.ExecuteNonQuery();
        }
        else
        {
            var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO ActivityRecords (ProcessName, WindowTitle, Type, StartTime, EndTime, Justification)
                VALUES ($processName, $windowTitle, $type, $startTime, $endTime, $justification)
                """;
            insert.Parameters.AddWithValue("$processName", record.ProcessName);
            insert.Parameters.AddWithValue("$windowTitle", record.WindowTitle);
            insert.Parameters.AddWithValue("$type", (int)record.Type);
            insert.Parameters.AddWithValue("$startTime", record.StartTime.ToString("O"));
            insert.Parameters.AddWithValue("$endTime", record.EndTime.ToString("O"));
            insert.Parameters.AddWithValue("$justification", (object?)record.Justification ?? DBNull.Value);
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
        command.CommandText = """
            SELECT Id, ProcessName, WindowTitle, Type, StartTime, EndTime, Justification
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
        command.CommandText = """
            SELECT Id, ProcessName, WindowTitle, Type, StartTime, EndTime, Justification
            FROM ActivityRecords
            WHERE StartTime >= $start AND StartTime < $end
            ORDER BY Id ASC
            """;
        command.Parameters.AddWithValue("$start", start.ToString("O"));
        command.Parameters.AddWithValue("$end", end.ToString("O"));
        return ReadAll(command);
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
        command.CommandText = """
            SELECT Id, ProcessName, WindowTitle, Type, StartTime, EndTime, Justification
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
        Justification = reader.IsDBNull(6) ? null : reader.GetString(6)
    };
}
