using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Infrastructure.Sync;

/// <summary>
/// Implementação HTTP de <see cref="ISyncApiClient"/>. Status 2xx = sucesso; 4xx = falha
/// permanente (não adianta reenviar o mesmo lote); 5xx, timeout ou erro de rede = falha
/// transitória (o <see cref="OneMonitor.Agent.Core.Services.SyncService"/> decide retentar).
/// </summary>
public class HttpSyncApiClient : ISyncApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly string _eventsPath;

    // Precisa casar com o Route Handler real: src/web-dashboard/app/api/sync/route.ts (-> /api/sync).
    public HttpSyncApiClient(HttpClient httpClient, string eventsPath = "api/sync")
    {
        _httpClient = httpClient;
        _eventsPath = eventsPath;
    }

    public async Task<SyncResponse> SendBatchAsync(SyncBatch batch, CancellationToken cancellationToken)
    {
        try
        {
            using var content = JsonContent.Create(batch, options: JsonOptions);
            using var response = await _httpClient.PostAsync(_eventsPath, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return SyncResponse.Success();
            }

            var isTransient =
                (int)response.StatusCode >= 500 ||
                response.StatusCode == HttpStatusCode.RequestTimeout ||
                response.StatusCode == HttpStatusCode.TooManyRequests;

            var error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            return isTransient ? SyncResponse.Transient(error) : SyncResponse.Permanent(error);
        }
        catch (HttpRequestException ex)
        {
            return SyncResponse.Transient(ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout do HttpClient (não foi o chamador que cancelou).
            return SyncResponse.Transient($"Timeout: {ex.Message}");
        }
    }
}
