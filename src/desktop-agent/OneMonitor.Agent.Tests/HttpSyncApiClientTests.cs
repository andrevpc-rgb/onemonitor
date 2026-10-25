using System.Net;
using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Infrastructure.Sync;
using OneMonitor.Agent.Tests.Fakes;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class HttpSyncApiClientTests
{
    private static readonly SyncBatch SampleBatch = new(
        "device-1",
        new List<ActivitySyncEvent>
        {
            new("evt-1", "device-1", "chrome", "Tab", "Active",
                new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 1, 15, 9, 5, 0, TimeSpan.Zero),
                null)
        });

    [Fact]
    public async Task SendBatchAsync_OnHttp200_ReturnsSuccess()
    {
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.OK);
        var client = CreateClient(handler);

        var response = await client.SendBatchAsync(SampleBatch, CancellationToken.None);

        Assert.Equal(SyncOutcome.Success, response.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task SendBatchAsync_OnTransientStatusCodes_ReturnsTransientFailure(HttpStatusCode statusCode)
    {
        var handler = StubHttpMessageHandler.ReturningStatus(statusCode);
        var client = CreateClient(handler);

        var response = await client.SendBatchAsync(SampleBatch, CancellationToken.None);

        Assert.Equal(SyncOutcome.TransientFailure, response.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task SendBatchAsync_OnPermanentStatusCodes_ReturnsPermanentFailure(HttpStatusCode statusCode)
    {
        var handler = StubHttpMessageHandler.ReturningStatus(statusCode);
        var client = CreateClient(handler);

        var response = await client.SendBatchAsync(SampleBatch, CancellationToken.None);

        Assert.Equal(SyncOutcome.PermanentFailure, response.Outcome);
    }

    [Fact]
    public async Task SendBatchAsync_OnNetworkException_ReturnsTransientFailure()
    {
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("conexão recusada"));
        var client = CreateClient(handler);

        var response = await client.SendBatchAsync(SampleBatch, CancellationToken.None);

        Assert.Equal(SyncOutcome.TransientFailure, response.Outcome);
    }

    [Fact]
    public async Task SendBatchAsync_SerializesDeviceIdAndEvents()
    {
        var handler = StubHttpMessageHandler.ReturningStatus(HttpStatusCode.OK);
        var client = CreateClient(handler);

        await client.SendBatchAsync(SampleBatch, CancellationToken.None);

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"deviceId\":\"device-1\"", handler.LastRequestBody);
        Assert.Contains("\"eventId\":\"evt-1\"", handler.LastRequestBody);
        Assert.Contains("\"processName\":\"chrome\"", handler.LastRequestBody);
    }

    private static HttpSyncApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        return new HttpSyncApiClient(httpClient);
    }
}
