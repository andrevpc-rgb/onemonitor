using Microsoft.Data.Sqlite;
using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Infrastructure.Persistence;
using OneMonitor.Agent.Tests.Fakes;
using Xunit;

namespace OneMonitor.Agent.Tests;

/// <summary>
/// Simula o reinício do agente: um novo SqliteActivityRepository (e um novo ActivityTracker)
/// apontando para o mesmo arquivo de banco deve enxergar os dados já persistidos e tratar
/// corretamente a lacuna de tempo causada pelo reinício.
/// </summary>
public class SqliteActivityRepositoryRecoveryTests : IDisposable
{
    private readonly string _databasePath;

    public SqliteActivityRepositoryRecoveryTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"onemonitor-tests-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public void NewRepositoryInstance_ReadsDataWrittenBeforeRestart()
    {
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        var repositoryBeforeRestart = new SqliteActivityRepository(_databasePath);
        repositoryBeforeRestart.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "devenv",
            WindowTitle = "Solution - Visual Studio",
            Type = ActivityType.Active,
            StartTime = start,
            EndTime = start.AddMinutes(15)
        });

        // "Reinício": nova instância do repositório apontando para o mesmo arquivo.
        var repositoryAfterRestart = new SqliteActivityRepository(_databasePath);

        var all = repositoryAfterRestart.GetAllRecords();
        var saved = Assert.Single(all);
        Assert.Equal("devenv", saved.ProcessName);
        Assert.Equal(TimeSpan.FromMinutes(15), saved.Duration);
    }

    [Fact]
    public void TrackerAfterRestart_DoesNotMergeAcrossTimeGap()
    {
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

        var repositoryBeforeRestart = new SqliteActivityRepository(_databasePath);
        repositoryBeforeRestart.SaveClosedRecord(new ActivityRecord
        {
            ProcessName = "devenv",
            WindowTitle = "Solution - Visual Studio",
            Type = ActivityType.Active,
            StartTime = start,
            EndTime = start.AddMinutes(10)
        });

        // Agente reinicia 5 minutos depois: novo repositório, novo tracker, mesma janela ativa.
        var restartTime = start.AddMinutes(15);
        var repositoryAfterRestart = new SqliteActivityRepository(_databasePath);
        var clock = new ManualClock(restartTime);
        var windowProvider = new FakeActiveWindowProvider { Snapshot = new ActiveWindowSnapshot("devenv", "Solution - Visual Studio") };
        var idleProvider = new FakeIdleTimeProvider();
        var tracker = new ActivityTracker(windowProvider, idleProvider, repositoryAfterRestart, clock, TimeSpan.FromMinutes(5));

        tracker.Tick();
        clock.Advance(TimeSpan.FromMinutes(5));
        tracker.Flush();

        var all = repositoryAfterRestart.GetAllRecords();

        // Duas linhas distintas: a lacuna de 5 minutos do reinício não é fundida.
        Assert.Equal(2, all.Count);
        Assert.Equal(TimeSpan.FromMinutes(10), all[0].Duration);
        Assert.Equal(TimeSpan.FromMinutes(5), all[1].Duration);
        Assert.True(all[1].StartTime > all[0].EndTime);
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
