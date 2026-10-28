using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Abstractions;

public enum SyncOutcome
{
    /// <summary>O backend confirmou o recebimento do lote.</summary>
    Success,

    /// <summary>Falha que pode se resolver sozinha (rede, timeout, 5xx) — vale repetir.</summary>
    TransientFailure,

    /// <summary>Falha que não vai se resolver repetindo o mesmo lote (ex.: 401, 400).</summary>
    PermanentFailure
}

public sealed record SyncResponse(SyncOutcome Outcome, string? Error = null, OrganizationSettings? Settings = null)
{
    /// <summary>
    /// <paramref name="settings"/> vem do corpo da resposta do heartbeat (ver
    /// HttpSyncApiClient) — pode ser null se o backend não devolveu nada (versão antiga da
    /// API, ou corpo vazio/malformado), nesse caso quem chama simplesmente não atualiza a
    /// política vigente.
    /// </summary>
    public static SyncResponse Success(OrganizationSettings? settings = null) => new(SyncOutcome.Success, null, settings);

    public static SyncResponse Transient(string error) => new(SyncOutcome.TransientFailure, error);

    public static SyncResponse Permanent(string error) => new(SyncOutcome.PermanentFailure, error);
}

/// <summary>
/// Envia um lote de eventos ao backend. Implementações concretas (HTTP, etc.) ficam na
/// Infrastructure — o Core só conhece este contrato, o que mantém o SyncService testável
/// sem rede real.
/// </summary>
public interface ISyncApiClient
{
    Task<SyncResponse> SendBatchAsync(SyncBatch batch, CancellationToken cancellationToken);
}
