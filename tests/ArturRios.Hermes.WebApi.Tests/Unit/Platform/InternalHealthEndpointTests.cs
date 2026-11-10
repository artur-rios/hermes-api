using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ArturRios.Hermes.WebApi.Platform;
using ArturRios.Messaging.Email;
using ArturRios.Output;
using ArturRios.Util.Test.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

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
        int port, HttpMessageHandler? mailgun, Mock<IEmailService>? email = null, bool configured = true) =>
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

                if (email is not null)
                {
                    services.AddTransient(_ => email.Object);
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
    public async Task GivenAnyFailure_WhenRequestedOnMetricsPort_ThenNoEmailIsSent()
    {
        // Given
        var email = new Mock<IEmailService>();
        email.Setup(service => service.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessOutput());
        using var factory = Application(MetricsSetup.Port, new Refusing(), email);

        // When
        await factory.CreateClient().GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);

        // Then
        email.Verify(
            service => service.SendEmailAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
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
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class Refusing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException(
                Secret, new SocketException((int)SocketError.ConnectionRefused), HttpStatusCode.ServiceUnavailable);
    }
}
