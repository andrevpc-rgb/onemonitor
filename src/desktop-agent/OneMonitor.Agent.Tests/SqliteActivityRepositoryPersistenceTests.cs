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

        // Meio-dia local, 24h de diferença: cai em dois dias de calendário local
        // distintos independentemente do fuso horário da máquina que roda o teste.
        var day1Local = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Local);
        var day2Local = new DateTime(2026, 1, 16, 12, 0, 0, DateTimeKind.Local);
        var day1 = new DateTimeOffset(day1Local).ToUniversalTime();
        var day2 = new DateTimeOffset(day2Local).ToUniversalTime();

        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "a", WindowTitle = "", Type = ActivityType.Active, StartTime = day1, EndTime = day1.AddMinutes(5)
        });
        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "b", WindowTitle = "", Type = ActivityType.Active, StartTime = day2, EndTime = day2.AddMinutes(5)
        });

        var day1Records = repository.GetRecordsForDate(DateOnly.FromDateTime(day1Local));
        var day2Records = repository.GetRecordsForDate(DateOnly.FromDateTime(day2Local));

        Assert.Single(day1Records);
        Assert.Equal("a", day1Records[0].ProcessName);
        Assert.Single(day2Records);
        Assert.Equal("b", day2Records[0].ProcessName);
    }

    [Fact]
    public void GetRecordsForDate_UsesLocalCalendarDayBoundaries_NotUtcMidnight()
    {
        var repository = new SqliteActivityRepository(_databasePath);
        var localDate = new DateOnly(2026, 1, 15);

        var localMidnight = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var utcMidnight = new DateTimeOffset(localMidnight).ToUniversalTime();

        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "inside",
            WindowTitle = "",
            Type = ActivityType.Active,
            StartTime = utcMidnight,
            EndTime = utcMidnight.AddMinutes(1)
        });
        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "before-midnight",
            WindowTitle = "",
            Type = ActivityType.Active,
            StartTime = utcMidnight.AddMilliseconds(-1),
            EndTime = utcMidnight
        });

        var records = repository.GetRecordsForDate(localDate);

        var record = Assert.Single(records);
        Assert.Equal("inside", record.ProcessName);
    }

    [Fact]
    public void SaveClosedRecord_ReturnsAnIdUsableToSetJustification()
    {
        var repository = new SqliteActivityRepository(_databasePath);
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        var id = repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "(idle)",
            WindowTitle = "",
            Type = ActivityType.Idle,
            StartTime = start,
            EndTime = start.AddMinutes(12)
        });

        repository.SetJustification(id, "Reunião Presencial");

        var saved = Assert.Single(repository.GetAllRecords());
        Assert.Equal("Reunião Presencial", saved.Justification);
    }

    [Fact]
    public void GetAllRecords_WithoutJustification_ReturnsNull()
    {
        var repository = new SqliteActivityRepository(_databasePath);
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        repository.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "chrome", WindowTitle = "", Type = ActivityType.Active, StartTime = start, EndTime = start.AddMinutes(5)
        });

        var saved = Assert.Single(repository.GetAllRecords());
        Assert.Null(saved.Justification);
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
