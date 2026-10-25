using Microsoft.Data.Sqlite;
using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Infrastructure.Persistence;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class SqliteActivityRepositoryPersistenceTests : IDisposable
{
    private readonly string _databasePath;

    public SqliteActivityRepositoryPersistenceTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"onemonitor-tests-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public void SaveClosedRecord_PersistsRecordThatCanBeReadBack()
    {
        var repository = new SqliteActivityRepository(_databasePath);
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "devenv",
            WindowTitle = "Solution - Visual Studio",
            Type = ActivityType.Active,
            StartTime = start,
            EndTime = start.AddMinutes(10)
        });

        var all = repository.GetAllRecords();
        var saved = Assert.Single(all);
        Assert.Equal("devenv", saved.ProcessName);
        Assert.Equal(TimeSpan.FromMinutes(10), saved.Duration);
    }

    [Fact]
    public void SaveClosedRecord_MergesContiguousIdenticalRecords()
    {
        var repository = new SqliteActivityRepository(_databasePath);
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "chrome",
            WindowTitle = "Tab - Chrome",
            Type = ActivityType.Active,
            StartTime = start,
            EndTime = start.AddSeconds(2)
        });
        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "chrome",
            WindowTitle = "Tab - Chrome",
            Type = ActivityType.Active,
            StartTime = start.AddSeconds(2),
            EndTime = start.AddSeconds(4)
        });

        var all = repository.GetAllRecords();
        var merged = Assert.Single(all);
        Assert.Equal(TimeSpan.FromSeconds(4), merged.Duration);
    }

    [Fact]
    public void GetRecordsForDate_ReturnsOnlyRecordsStartingOnThatDate()
    {
        var repository = new SqliteActivityRepository(_databasePath);
        var day1 = new DateTimeOffset(2026, 1, 15, 23, 0, 0, TimeSpan.Zero);
        var day2 = new DateTimeOffset(2026, 1, 16, 1, 0, 0, TimeSpan.Zero);

        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "a", WindowTitle = "", Type = ActivityType.Active, StartTime = day1, EndTime = day1.AddMinutes(5)
        });
        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "b", WindowTitle = "", Type = ActivityType.Active, StartTime = day2, EndTime = day2.AddMinutes(5)
        });

        var day1Records = repository.GetRecordsForDate(DateOnly.FromDateTime(day1.UtcDateTime));
        var day2Records = repository.GetRecordsForDate(DateOnly.FromDateTime(day2.UtcDateTime));

        Assert.Single(day1Records);
        Assert.Equal("a", day1Records[0].ProcessName);
        Assert.Single(day2Records);
        Assert.Equal("b", day2Records[0].ProcessName);
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite mantém um pool de conexões nativas; sem limpá-lo, o
        // arquivo continua bloqueado mesmo depois de cada SqliteConnection ser descartada.
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
