using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Tests.Fakes;

public class FakeSyncApiClient : ISyncApiClient
{
    private readonly Queue<SyncResponse> _responses;

    public FakeSyncApiClient(params SyncResponse[] responses) =>
        _responses = new Queue<SyncResponse>(responses);

    public List<SyncBatch> ReceivedBatches { get; } = new();

    public int CallCount { get; private set; }

    /// <summary>Resposta usada quando a fila de respostas pré-configuradas se esgota.</summary>
    public SyncResponse DefaultResponse { get; set; } = SyncResponse.Success();

    public Task<SyncResponse> SendBatchAsync(SyncBatch batch, CancellationToken cancellationToken)
    {
        CallCount++;
        ReceivedBatches.Add(batch);
        var response = _responses.Count > 0 ? _responses.Dequeue() : DefaultResponse;
        return Task.FromResult(response);
    }
}
