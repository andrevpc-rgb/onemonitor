using System.Net;

namespace OneMonitor.Agent.Tests.Fakes;

/// <summary>
/// Handler de HttpClient controlado pelo teste: devolve a resposta configurada (ou lança a
/// exceção configurada) sem nenhuma chamada de rede real.
/// </summary>
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage>? _responseFactory;
    private readonly Exception? _exceptionToThrow;

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    public static StubHttpMessageHandler ReturningStatus(HttpStatusCode statusCode) =>
        new(_ => new HttpResponseMessage(statusCode));

    public static StubHttpMessageHandler Throwing(Exception exception) => new(exception);

    private StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        _responseFactory = responseFactory;

    private StubHttpMessageHandler(Exception exceptionToThrow) => _exceptionToThrow = exceptionToThrow;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        return _responseFactory!(request);
    }
}
