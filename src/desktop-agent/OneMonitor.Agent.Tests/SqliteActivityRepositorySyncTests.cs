using Microsoft.Data.Sqlite;
using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Infrastructure.Persistence;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class SqliteActivityRepositorySyncTests : IDisposable
{
    private readonly string _databasePath;

    public SqliteActivityRepositorySyncTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"onemonitor-tests-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public void SaveClosedRecord_AssignsEventIdWhenMissing()
    {
        var repository = new SqliteActivityRepository(_databasePath);
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "chrome", WindowTitle = "", Type = ActivityType.Active, StartTime = start, EndTime = start.AddMinutes(1)
        });

        var saved = Assert.Single(repository.GetAllRecords());
        Assert.False(string.IsNullOrEmpty(saved.EventId));
    }

    [Fact]
    public void GetPendingSyncRecords_ReturnsOnlyRecordsWithoutSyncedAt()
    {
        var repository = new SqliteActivityRepository(_databasePath);
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        var id1 = repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "chrome", WindowTitle = "", Type = ActivityType.Active, StartTime = start, EndTime = start.AddMinutes(1)
        });
        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "devenv", WindowTitle = "", Type = ActivityType.Active, StartTime = start.AddMinutes(5), EndTime = start.AddMinutes(6)
        });

        repository.MarkSynced(new[] { id1 }, start.AddMinutes(10));

        var pending = repository.GetPendingSyncRecords(10);

        var pendingRecord = Assert.Single(pending);
        Assert.Equal("devenv", pendingRecord.ProcessName);
    }

    [Fact]
    public void GetPendingSyncRecords_RespectsMaxCount()
    {
        var repository = new SqliteActivityRepository(_databasePath);
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 5; i++)
        {
            repository.SaveClosedRecord(new ActivityRecord
            {
                ProcessName = $"app-{i}", WindowTitle = "", Type = ActivityType.Active,
                StartTime = start.AddMinutes(i * 10), EndTime = start.AddMinutes(i * 10 + 1)
            });
        }

        var pending = repository.GetPendingSyncRecords(2);

        Assert.Equal(2, pending.Count);
        Assert.Equal("app-0", pending[0].ProcessName);
        Assert.Equal("app-1", pending[1].ProcessName);
    }

    [Fact]
    public void MarkSynced_SetsSyncedAtOnAllGivenIds()
    {
        var repository = new SqliteActivityRepository(_databasePath);
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
        var syncedAt = start.AddHours(1);

        var id1 = repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "a", WindowTitle = "", Type = ActivityType.Active, StartTime = start, EndTime = start.AddMinutes(1)
        });
        var id2 = repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "b", WindowTitle = "", Type = ActivityType.Active, StartTime = start.AddMinutes(5), EndTime = start.AddMinutes(6)
        });

        repository.MarkSynced(new[] { id1, id2 }, syncedAt);

        var all = repository.GetAllRecords();
        Assert.All(all, r => Assert.Equal(syncedAt, r.SyncedAt));
    }

    [Fact]
    public void SaveClosedRecord_MergeResetsSyncedAt_SoExtendedPortionGetsResynced()
    {
        var repository = new SqliteActivityRepository(_databasePath);
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        var id = repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "chrome", WindowTitle = "Tab", Type = ActivityType.Active, StartTime = start, EndTime = start.AddSeconds(2)
        });
        repository.MarkSynced(new[] { id }, start.AddMinutes(1));

        // Mesma atividade, contígua -> funde com a linha já sincronizada.
        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "chrome", WindowTitle = "Tab", Type = ActivityType.Active, StartTime = start.AddSeconds(2), EndTime = start.AddSeconds(4)
        });

        var merged = Assert.Single(repository.GetAllRecords());
        Assert.Equal(TimeSpan.FromSeconds(4), merged.Duration);
        Assert.Null(merged.SyncedAt);
        Assert.Single(repository.GetPendingSyncRecords(10));
    }

    [Fact]
    public void OpeningRepository_BackfillsEventIdForPreExistingRowsWithoutIt()
    {
        // Simula um banco criado antes de EventId existir.
        using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            connection.Open();
            var create = connection.CreateCommand();
            create.CommandText = """
                CREATE TABLE ActivityRecords (
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

            var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO ActivityRecords (ProcessName, WindowTitle, Type, StartTime, EndTime)
                VALUES ('devenv', 'Solution', 0, '2026-01-15T09:00:00+00:00', '2026-01-15T09:10:00+00:00')
                """;
            insert.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        var repository = new SqliteActivityRepository(_databasePath);

        var record = Assert.Single(repository.GetAllRecords());
        Assert.False(string.IsNullOrEmpty(record.EventId));
        Assert.Null(record.SyncedAt);

        // E já entra na fila de sincronização normalmente.
        Assert.Single(repository.GetPendingSyncRecords(10));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
