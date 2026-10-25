using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Services;

/// <summary>
/// Orquestra a sincronização offline-first: busca registros pendentes no repositório,
/// monta um lote, envia via <see cref="ISyncApiClient"/> e marca como sincronizado só em
/// caso de sucesso confirmado. Não faz I/O diretamente — depende só de abstrações, o que
/// permite testar toda a lógica de lote/retentativa/backoff sem rede nem SQLite reais.
/// </summary>
public class SyncService
{
    private readonly IActivityRepository _repository;
    private readonly ISyncApiClient _apiClient;
    private readonly IClock _clock;
    private readonly IAsyncDelay _delay;
    private readonly ICategoryResolver _categoryResolver;
    private readonly string _deviceId;
    private readonly int _batchSize;
    private readonly int _maxRetries;

    public SyncService(
        IActivityRepository repository,
        ISyncApiClient apiClient,
        IClock clock,
        IAsyncDelay delay,
        ICategoryResolver categoryResolver,
        string deviceId,
        int batchSize = 50,
        int maxRetries = 3)
    {
        _repository = repository;
        _apiClient = apiClient;
        _clock = clock;
        _delay = delay;
        _categoryResolver = categoryResolver;
        _deviceId = deviceId;
        _batchSize = batchSize;
        _maxRetries = maxRetries;
    }

    /// <summary>
    /// Processa um único lote de registros pendentes (no máximo <c>batchSize</c>). Quando há
    /// mais pendências do que cabem em um lote, ciclos subsequentes (chamados periodicamente
    /// pelo host) drenam o restante — isso evita que um backlog grande trave uma única chamada.
    /// </summary>
    public async Task<SyncCycleResult> SyncPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = _repository.GetPendingSyncRecords(_batchSize);
        if (pending.Count == 0)
        {
            return SyncCycleResult.NoPendingEvents;
        }

        var batch = new SyncBatch(_deviceId, pending.Select(ToEvent).ToList());
        var response = await SendWithRetryAsync(batch, cancellationToken);

        if (response.Outcome == SyncOutcome.Success)
        {
            _repository.MarkSynced(pending.Select(r => r.Id).ToList(), _clock.UtcNow);
            return SyncCycleResult.Synced(pending.Count);
        }

        return SyncCycleResult.Failed(response.Error);
    }

    private async Task<SyncResponse> SendWithRetryAsync(SyncBatch batch, CancellationToken cancellationToken)
    {
        var attempt = 0;
        SyncResponse response;

        while (true)
        {
            response = await _apiClient.SendBatchAsync(batch, cancellationToken);

            if (response.Outcome != SyncOutcome.TransientFailure || attempt >= _maxRetries)
            {
                return response;
            }

            // Backoff exponencial: 1s, 2s, 4s, ...
            var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            attempt++;
            await _delay.Delay(backoff, cancellationToken);
        }
    }

    private ActivitySyncEvent ToEvent(ActivityRecord record) => new(
        record.EventId,
        _deviceId,
        record.ProcessName,
        record.WindowTitle,
        record.Type.ToString(),
        record.StartTime,
        record.EndTime,
        record.Justification,
        _categoryResolver.Resolve(record.ProcessName, record.WindowTitle));
}
