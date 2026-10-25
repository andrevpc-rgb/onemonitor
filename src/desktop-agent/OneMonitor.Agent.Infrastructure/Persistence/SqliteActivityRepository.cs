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
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ActivityRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProcessName TEXT NOT NULL,
                WindowTitle TEXT NOT NULL,
                Type INTEGER NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public void SaveClosedRecord(ActivityRecord record)
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

        if (canMerge)
        {
            var update = connection.CreateCommand();
            update.CommandText = "UPDATE ActivityRecords SET EndTime = $endTime WHERE Id = $id";
            update.Parameters.AddWithValue("$endTime", record.EndTime.ToString("O"));
            update.Parameters.AddWithValue("$id", last!.Id);
            update.ExecuteNonQuery();
        }
        else
        {
            var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO ActivityRecords (ProcessName, WindowTitle, Type, StartTime, EndTime)
                VALUES ($processName, $windowTitle, $type, $startTime, $endTime)
                """;
            insert.Parameters.AddWithValue("$processName", record.ProcessName);
            insert.Parameters.AddWithValue("$windowTitle", record.WindowTitle);
            insert.Parameters.AddWithValue("$type", (int)record.Type);
            insert.Parameters.AddWithValue("$startTime", record.StartTime.ToString("O"));
            insert.Parameters.AddWithValue("$endTime", record.EndTime.ToString("O"));
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
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
            SELECT Id, ProcessName, WindowTitle, Type, StartTime, EndTime
            FROM ActivityRecords
            ORDER BY Id ASC
            """;
        return ReadAll(command);
    }

    public IReadOnlyList<ActivityRecord> GetRecordsForDate(DateOnly date)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);

        using var connection = OpenConnection();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, ProcessName, WindowTitle, Type, StartTime, EndTime
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
            SELECT Id, ProcessName, WindowTitle, Type, StartTime, EndTime
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
        EndTime = DateTimeOffset.Parse(reader.GetString(5))
    };
}
