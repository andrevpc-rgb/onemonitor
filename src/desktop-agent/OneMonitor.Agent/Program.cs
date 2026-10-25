using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Infrastructure.Categorization;
using OneMonitor.Agent.Infrastructure.Persistence;
using OneMonitor.Agent.Infrastructure.Reporting;
using OneMonitor.Agent.Infrastructure.Time;
using OneMonitor.Agent.Infrastructure.Windows;

namespace OneMonitor.Agent;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var settings = AppSettings.Load();

        var repository = new SqliteActivityRepository(settings.ResolvedDatabasePath);

        var tracker = new ActivityTracker(
            new ActiveWindowProvider(),
            new IdleTimeProvider(),
            repository,
            new SystemClock(),
            TimeSpan.FromMinutes(settings.InactivityThresholdMinutes));

        var categoryResolver = CategoryRulesLoader.LoadFromFile(settings.ResolvedCategoriesConfigPath);

        var reportService = new DailyReportService(
            repository,
            new ReportFileWriter(),
            settings.ResolvedReportOutputDirectory,
            categoryResolver);

        using var context = new TrayApplicationContext(
            tracker,
            reportService,
            TimeSpan.FromSeconds(settings.PollingIntervalSeconds));

        Application.Run(context);
    }
}
