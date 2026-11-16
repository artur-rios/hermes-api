using System.Net;
using System.Net.Http.Headers;

namespace ArturRios.Hermes.WebApi.Tests;

/// <summary>
/// Stands in for Mailgun on the health check's call: answers or fails as the test says, records what it was asked,
/// and can hold every answer until the test opens <see cref="Gate"/>.
/// </summary>
internal sealed class HealthProbeHandler(Func<HttpResponseMessage> answer) : HttpMessageHandler
{
    private readonly List<CapturedRequest> _requests = [];

    public static HealthProbeHandler Answering(HttpStatusCode status) => new(() => new HttpResponseMessage(status));

    public static HealthProbeHandler Failing(Exception failure) => new(() => throw failure);

    /// <summary>What the next call gets; a test may change it between calls.</summary>
    public Func<HttpResponseMessage> Answer { get; set; } = answer;

    public TaskCompletionSource? Gate { get; init; }

    public int Calls
    {
        get
        {
            lock (_requests)
            {
                return _requests.Count;
            }
        }
    }

    public IReadOnlyList<CapturedRequest> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_requests)
        {
            _requests.Add(new CapturedRequest(
                request.Method, request.RequestUri?.ToString(), request.Headers.Authorization, request.Content is not null));
        }

        if (Gate is not null)
        {
            await Gate.Task;
        }

        return Answer();
    }
}

internal sealed record CapturedRequest(HttpMethod Method, string? Uri, AuthenticationHeaderValue? Authorization, bool HasContent);

/// <summary>Hands out clients that all go through one handler, as the factory does for a named client.</summary>
internal sealed class HandlerClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
