using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Infrastructure.Categorization;
using OneMonitor.Agent.Infrastructure.Persistence;
using OneMonitor.Agent.Infrastructure.Reporting;
using OneMonitor.Agent.Infrastructure.Sync;
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

        var syncService = BuildSyncServiceIfEnabled(settings, repository);

        using var context = new TrayApplicationContext(
            tracker,
            reportService,
            TimeSpan.FromSeconds(settings.PollingIntervalSeconds),
            syncService,
            TimeSpan.FromSeconds(settings.Sync.SyncIntervalSeconds));

        Application.Run(context);
    }

    private static SyncService? BuildSyncServiceIfEnabled(AppSettings settings, SqliteActivityRepository repository)
    {
        if (!settings.Sync.Enabled)
        {
            return null;
        }

        var deviceId = DeviceIdentityProvider.GetOrCreateDeviceId(settings.ResolvedDeviceIdFilePath);

        var httpClient = new HttpClient { BaseAddress = new Uri(settings.Sync.ApiBaseUrl) };
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", settings.Sync.ApiKey);
        httpClient.DefaultRequestHeaders.Add("X-Device-Id", deviceId);

        var apiClient = new HttpSyncApiClient(httpClient);

        return new SyncService(
            repository,
            apiClient,
            new SystemClock(),
            new TaskDelay(),
            deviceId,
            settings.Sync.BatchSize,
            settings.Sync.MaxRetries);
    }
}
