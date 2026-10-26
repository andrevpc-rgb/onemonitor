using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using OneMonitor.Agent.Tests.Fakes;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class SyncServiceTests
{
    [Fact]
    public async Task SyncPendingAsync_WithNoPendingRecords_StillCallsApiToCarryHeartbeat()
    {
        // Diferente de uma simples "sincronização de atividade": o ciclo periódico também é
        // o heartbeat do agente, então precisa chamar a API mesmo sem nada pendente.
        var repository = new InMemoryActivityRepository();
        var apiClient = new FakeSyncApiClient();
        var service = new SyncService(repository, apiClient, new ManualClock(DateTimeOffset.UtcNow), new FakeAsyncDelay(), new FakeCategoryResolver(), "device-1");

        var result = await service.SyncPendingAsync();

        Assert.True(result.Success);
        Assert.Equal(0, result.EventsSynced);
        Assert.Equal(1, apiClient.CallCount);

        var batch = Assert.Single(apiClient.ReceivedBatches);
        Assert.Empty(batch.Events);
        Assert.NotNull(batch.Status);
        Assert.Equal(DeviceStatusEvent.Heartbeat, batch.Status!.State);
    }

    [Fact]
    public async Task SyncPendingAsync_WithPendingRecords_AlsoIncludesHeartbeatStatus()
    {
        var repository = new InMemoryActivityRepository();
        repository.SaveClosedRecord(new ActivityRecord
        {
            EventId = "evt-1", ProcessName = "chrome", WindowTitle = "", Type = ActivityType.Active,
            StartTime = DateTimeOffset.UtcNow, EndTime = DateTimeOffset.UtcNow.AddMinutes(1)
        });

        var apiClient = new FakeSyncApiClient(SyncResponse.Success());
        var service = new SyncService(repository, apiClient, new ManualClock(DateTimeOffset.UtcNow), new FakeAsyncDelay(), new FakeCategoryResolver(), "device-1");

        await service.SyncPendingAsync();

        var batch = Assert.Single(apiClient.ReceivedBatches);
        Assert.Single(batch.Events);
        Assert.Equal(DeviceStatusEvent.Heartbeat, batch.Status!.State);
    }

    [Theory]
    [InlineData(DeviceStatusEvent.Started)]
    [InlineData(DeviceStatusEvent.Stopped)]
    public async Task SendStatusAsync_SendsEmptyEventsWithTheGivenState(string state)
    {
        var repository = new InMemoryActivityRepository();
        var apiClient = new FakeSyncApiClient(SyncResponse.Success());
        var clock = new ManualClock(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));
        var service = new SyncService(repository, apiClient, clock, new FakeAsyncDelay(), new FakeCategoryResolver(), "device-1");

        var result = await service.SendStatusAsync(state);

        Assert.True(result.Success);
        var batch = Assert.Single(apiClient.ReceivedBatches);
        Assert.Equal("device-1", batch.DeviceId);
        Assert.Empty(batch.Events);
        Assert.Equal(state, batch.Status!.State);
        Assert.Equal(clock.UtcNow, batch.Status.Timestamp);
    }

    [Fact]
    public async Task SendStatusAsync_OnTransientFailure_RetriesWithBackoffLikeARegularBatch()
    {
        var repository = new InMemoryActivityRepository();
        var apiClient = new FakeSyncApiClient(SyncResponse.Transient("timeout"), SyncResponse.Success());
        var delay = new FakeAsyncDelay();
        var service = new SyncService(repository, apiClient, new ManualClock(DateTimeOffset.UtcNow), delay, new FakeCategoryResolver(), "device-1", maxRetries: 3);

        var result = await service.SendStatusAsync(DeviceStatusEvent.Stopped);

        Assert.True(result.Success);
        Assert.Equal(2, apiClient.CallCount);
        Assert.Single(delay.RequestedDelays);
    }

    [Fact]
    public async Task SyncPendingAsync_OnSuccess_MarksRecordsAsSyncedAndSendsCorrectPayload()
    {
        var repository = new InMemoryActivityRepository();
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
        repository.SaveClosedRecord(new ActivityRecord
        {
            EventId = "evt-1", ProcessName = "chrome", WindowTitle = "Tab", Type = ActivityType.Active,
            StartTime = start, EndTime = start.AddMinutes(5)
        });

        var apiClient = new FakeSyncApiClient(SyncResponse.Success());
        var clock = new ManualClock(start.AddHours(1));
        var categoryResolver = new FakeCategoryResolver(new Dictionary<string, string> { ["chrome"] = "Navegação" });
        var service = new SyncService(repository, apiClient, clock, new FakeAsyncDelay(), categoryResolver, "device-1");

        var result = await service.SyncPendingAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.EventsSynced);
        Assert.Empty(repository.GetPendingSyncRecords(10));

        var batch = Assert.Single(apiClient.ReceivedBatches);
        Assert.Equal("device-1", batch.DeviceId);
        var sentEvent = Assert.Single(batch.Events);
        Assert.Equal("evt-1", sentEvent.EventId);
        Assert.Equal("Navegação", sentEvent.Category);
        Assert.Equal("chrome", sentEvent.ProcessName);
        Assert.Equal("Active", sentEvent.Type);
    }

    [Fact]
    public async Task SyncPendingAsync_OnPermanentFailure_LeavesRecordsPendingAndDoesNotRetry()
    {
        var repository = new InMemoryActivityRepository();
        repository.SaveClosedRecord(new ActivityRecord
        {
            EventId = "evt-1", ProcessName = "chrome", WindowTitle = "", Type = ActivityType.Active,
            StartTime = DateTimeOffset.UtcNow, EndTime = DateTimeOffset.UtcNow.AddMinutes(1)
        });

        var apiClient = new FakeSyncApiClient(SyncResponse.Permanent("401 Unauthorized"));
        var service = new SyncService(repository, apiClient, new ManualClock(DateTimeOffset.UtcNow), new FakeAsyncDelay(), new FakeCategoryResolver(), "device-1", maxRetries: 3);

        var result = await service.SyncPendingAsync();

        Assert.False(result.Success);
        Assert.Single(repository.GetPendingSyncRecords(10));
        Assert.Equal(1, apiClient.CallCount); // não insiste numa falha permanente.
    }

    [Fact]
    public async Task SyncPendingAsync_OnTransientFailure_RetriesWithExponentialBackoffUntilMaxRetries()
    {
        var repository = new InMemoryActivityRepository();
        repository.SaveClosedRecord(new ActivityRecord
        {
            EventId = "evt-1", ProcessName = "chrome", WindowTitle = "", Type = ActivityType.Active,
            StartTime = DateTimeOffset.UtcNow, EndTime = DateTimeOffset.UtcNow.AddMinutes(1)
        });

        var apiClient = new FakeSyncApiClient { DefaultResponse = SyncResponse.Transient("timeout") };
        var delay = new FakeAsyncDelay();
        var service = new SyncService(repository, apiClient, new ManualClock(DateTimeOffset.UtcNow), delay, new FakeCategoryResolver(), "device-1", maxRetries: 3);

        var result = await service.SyncPendingAsync();

        Assert.False(result.Success);
        Assert.Single(repository.GetPendingSyncRecords(10)); // continua pendente, para o próximo ciclo tentar de novo.
        Assert.Equal(4, apiClient.CallCount); // 1 tentativa inicial + 3 retentativas.
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) }, delay.RequestedDelays);
    }

    [Fact]
    public async Task SyncPendingAsync_SucceedsAfterOneTransientFailure_StopsRetryingAndMarksSynced()
    {
        var repository = new InMemoryActivityRepository();
        repository.SaveClosedRecord(new ActivityRecord
        {
            EventId = "evt-1", ProcessName = "chrome", WindowTitle = "", Type = ActivityType.Active,
            StartTime = DateTimeOffset.UtcNow, EndTime = DateTimeOffset.UtcNow.AddMinutes(1)
        });

        var apiClient = new FakeSyncApiClient(SyncResponse.Transient("timeout"), SyncResponse.Success());
        var delay = new FakeAsyncDelay();
        var service = new SyncService(repository, apiClient, new ManualClock(DateTimeOffset.UtcNow), delay, new FakeCategoryResolver(), "device-1", maxRetries: 3);

        var result = await service.SyncPendingAsync();

        Assert.True(result.Success);
        Assert.Equal(2, apiClient.CallCount);
        Assert.Single(delay.RequestedDelays);
        Assert.Empty(repository.GetPendingSyncRecords(10));
    }

    [Fact]
    public async Task SyncPendingAsync_RespectsBatchSize()
    {
        var repository = new InMemoryActivityRepository();
        var start = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 5; i++)
        {
            repository.SaveClosedRecord(new ActivityRecord
            {
                EventId = $"evt-{i}",
                ProcessName = $"app-{i}",
                WindowTitle = "",
                Type = ActivityType.Active,
                StartTime = start.AddMinutes(i * 10),
                EndTime = start.AddMinutes(i * 10 + 1)
            });
        }

        var apiClient = new FakeSyncApiClient(SyncResponse.Success());
        var service = new SyncService(repository, apiClient, new ManualClock(start.AddHours(1)), new FakeAsyncDelay(), new FakeCategoryResolver(), "device-1", batchSize: 2);

        var result = await service.SyncPendingAsync();

        Assert.Equal(2, result.EventsSynced);
        var batch = Assert.Single(apiClient.ReceivedBatches);
        Assert.Equal(2, batch.Events.Count);
        Assert.Equal(3, repository.GetPendingSyncRecords(10).Count); // os outros 3 ficam para o próximo ciclo.
    }
}
