using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ArturRios.Hermes.WebApi.Platform;
using ArturRios.Util.Test.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Platform;

/// <summary>
/// The report as it is requested on each port. The in-memory server has no sockets, so a startup filter sets the local
/// port ahead of the real pipeline, as <c>MetricsPortTests</c> does.
/// </summary>
[Collection(ProcessStateCollection.Name)]
public class InternalHealthEndpointTests
{
    private const string Secret = "secret-detail-that-must-not-leak";

    private static WebApplicationFactory<Program> Application(
        int port, HttpMessageHandler? mailgun, TestClock? clock = null, bool configured = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("local")
            .ConfigureAppConfiguration((_, configuration) =>
            {
                if (configured)
                {
                    configuration.AddInMemoryCollection(new ClientSettings().WithMailgun().Values);
                }
            })
            .ConfigureTestServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new ArrivesOnPort(port));

                if (mailgun is not null)
                {
                    services.AddHttpClient(InternalHealthEndpoint.ClientName).ConfigurePrimaryHttpMessageHandler(() => mailgun);
                }

                if (clock is not null)
                {
                    services.AddSingleton(clock.Provider);
                }
            }));

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return JsonDocument.Parse(body).RootElement;
    }

    [UnitFact]
    public async Task GivenMailgunReachable_WhenRequestedOnMetricsPort_ThenHealthy200()
    {
        // Given
        using var factory = Application(MetricsSetup.Port, new Answering(HttpStatusCode.OK));

        // When
        var response = await factory.CreateClient().GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);
        var report = await ReadAsync(response);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", report.GetProperty("status").GetString());
        Assert.Equal("Mailgun", report.GetProperty("services")[0].GetProperty("name").GetString());
        Assert.Equal("Healthy", report.GetProperty("services")[0].GetProperty("status").GetString());
        Assert.False(report.GetProperty("services")[0].TryGetProperty("reason", out _));
    }

    [UnitFact]
    public async Task GivenMailgunAnswers500_WhenRequestedOnMetricsPort_ThenStillHealthy()
    {
        // Given
        using var factory = Application(MetricsSetup.Port, new Answering(HttpStatusCode.InternalServerError));

        // When
        var response = await factory.CreateClient().GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);
        var report = await ReadAsync(response);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", report.GetProperty("status").GetString());
    }

    [UnitFact]
    public async Task GivenMailgunRefused_WhenRequestedOnMetricsPort_ThenDegraded200WithReason()
    {
        // Given
        using var factory = Application(MetricsSetup.Port, new Refusing());

        // When
        var response = await factory.CreateClient().GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """{"status":"Degraded","services":[{"name":"Mailgun","status":"Unhealthy","reason":"connection refused"}]}""",
            body);
        Assert.DoesNotContain(Secret, body);
    }

    [UnitFact]
    public async Task GivenMailgunNotConfigured_WhenRequestedOnMetricsPort_ThenNotConfiguredAndNoCallMade()
    {
        // Given
        var mailgun = new Answering(HttpStatusCode.OK);
        using var factory = Application(MetricsSetup.Port, mailgun, configured: false);

        // When
        var response = await factory.CreateClient().GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);
        var report = await ReadAsync(response);

        // Then
        Assert.Equal("Healthy", report.GetProperty("status").GetString());
        Assert.Equal("NotConfigured", report.GetProperty("services")[0].GetProperty("status").GetString());
        Assert.Equal(0, mailgun.Calls);
    }

    [UnitFact]
    public async Task GivenApplicationPort_WhenRequested_Then404()
    {
        // Given
        var mailgun = new Answering(HttpStatusCode.OK);
        using var factory = Application(8080, mailgun);

        // When
        var response = await factory.CreateClient().GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, mailgun.Calls);
    }

    [UnitFact]
    public async Task GivenOtherPathOnMetricsPort_WhenRequested_Then404()
    {
        // Given
        using var factory = Application(MetricsSetup.Port, new Answering(HttpStatusCode.OK));

        // When
        var response = await factory.CreateClient().GetAsync("/internal/other", TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [UnitFact]
    public async Task GivenPostOnMetricsPort_WhenRequested_Then404()
    {
        // Given
        using var factory = Application(MetricsSetup.Port, new Answering(HttpStatusCode.OK));

        // When
        var response = await factory.CreateClient().PostAsync(InternalHealthEndpoint.Route, null, TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [UnitFact]
    public async Task GivenMailgunConfigured_WhenRequestedOnMetricsPort_ThenOneBareGetIsMade()
    {
        // Given
        var mailgun = new Answering(HttpStatusCode.OK);
        using var factory = Application(MetricsSetup.Port, mailgun);

        // When
        await factory.CreateClient().GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);

        // Then
        var request = Assert.Single(mailgun.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.mailgun.net/", request.Uri);
        Assert.Null(request.Authorization);
        Assert.False(request.HasContent);
    }

    [UnitFact]
    public async Task GivenConcurrentRequests_WhenRequestedOnMetricsPort_ThenOneOutboundCall()
    {
        // Given
        var mailgun = new Answering(HttpStatusCode.OK) { Gate = new TaskCompletionSource() };
        using var factory = Application(MetricsSetup.Port, mailgun);
        var client = factory.CreateClient();
        var token = TestContext.Current.CancellationToken;

        // When
        var first = client.GetAsync(InternalHealthEndpoint.Route, token);
        while (mailgun.Calls == 0)
        {
            await Task.Delay(10, token);
        }

        var second = client.GetAsync(InternalHealthEndpoint.Route, token);
        await Task.Delay(100, token);
        mailgun.Gate.SetResult();
        var responses = await Task.WhenAll(first, second);

        // Then
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1, mailgun.Calls);
    }

    [UnitFact]
    public async Task GivenARecentResult_WhenRequestedAgainWithinTheWindow_ThenNoNewCall()
    {
        // Given
        var clock = new TestClock(DateTimeOffset.Parse("2026-11-10T12:00:00Z"));
        var mailgun = new Answering(HttpStatusCode.OK);
        using var factory = Application(MetricsSetup.Port, mailgun, clock);
        var client = factory.CreateClient();
        await client.GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);

        // When
        clock.Now = clock.Now.AddSeconds(4);
        await client.GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(1, mailgun.Calls);
    }

    [UnitFact]
    public async Task GivenAnOldResult_WhenRequestedAfterTheWindow_ThenANewCallIsMade()
    {
        // Given
        var clock = new TestClock(DateTimeOffset.Parse("2026-11-10T12:00:00Z"));
        var mailgun = new Answering(HttpStatusCode.OK);
        using var factory = Application(MetricsSetup.Port, mailgun, clock);
        var client = factory.CreateClient();
        await client.GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);

        // When
        clock.Now = clock.Now.AddSeconds(5);
        await client.GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(2, mailgun.Calls);
    }

    [UnitFact]
    public void GivenUnknownException_WhenDescribed_ThenRequestFailedWithoutItsText()
    {
        // When
        var reason = HealthReason.From(new InvalidOperationException(Secret));

        // Then
        Assert.Equal("request failed", reason);
    }

    [UnitFact]
    public void GivenCancellation_WhenDescribed_ThenTimeout()
    {
        // When
        var reason = HealthReason.From(new TaskCanceledException());

        // Then
        Assert.Equal("timeout", reason);
    }

    [UnitTheory]
    [InlineData(SocketError.ConnectionRefused, "connection refused")]
    [InlineData(SocketError.HostNotFound, "name not resolved")]
    [InlineData(SocketError.NetworkUnreachable, "host unreachable")]
    [InlineData(SocketError.ConnectionReset, "connection reset")]
    public void GivenSocketError_WhenDescribed_ThenItsWord(SocketError error, string expected)
    {
        // When
        var reason = HealthReason.From(new HttpRequestException(Secret, new SocketException((int)error)));

        // Then
        Assert.Equal(expected, reason);
    }

    [UnitTheory]
    [InlineData(HttpRequestError.NameResolutionError, "name not resolved")]
    [InlineData(HttpRequestError.ConnectionError, "connection failed")]
    [InlineData(HttpRequestError.SecureConnectionError, "tls error")]
    [InlineData(HttpRequestError.ResponseEnded, "connection closed")]
    [InlineData(HttpRequestError.InvalidResponse, "invalid response")]
    [InlineData(HttpRequestError.Unknown, "request failed")]
    public void GivenRequestError_WhenDescribed_ThenItsWord(HttpRequestError error, string expected)
    {
        // When
        var reason = HealthReason.From(new HttpRequestException(error, Secret));

        // Then
        Assert.Equal(expected, reason);
    }

    private sealed class ArrivesOnPort(int port) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.LocalPort = port;

                return nextMiddleware();
            });

            next(app);
        };
    }

    private sealed class Answering(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls => Requests.Count;

        public List<Captured> Requests { get; } = [];

        public TaskCompletionSource? Gate { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add(new Captured(
                    request.Method, request.RequestUri?.ToString(), request.Headers.Authorization, request.Content is not null));
            }

            if (Gate is not null)
            {
                await Gate.Task;
            }

            return new HttpResponseMessage(status);
        }
    }

    internal sealed record Captured(
        HttpMethod Method, string? Uri, System.Net.Http.Headers.AuthenticationHeaderValue? Authorization, bool HasContent);

    private sealed class Refusing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException(
                Secret, new SocketException((int)SocketError.ConnectionRefused), HttpStatusCode.ServiceUnavailable);
    }
}
