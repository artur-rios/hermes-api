using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ArturRios.Hermes.WebApi.Platform;
using ArturRios.Util.Test.Attributes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Hermes.WebApi.Tests.Functional;

/// <summary>
/// <c>GET /internal/health</c> as it is requested on each port, through the real pipeline. The check behind it, with
/// its cache, its one call in flight and its reason words, is unit-tested in <c>MailgunHealthTests</c> and
/// <c>HealthReasonTests</c>.
/// </summary>
[Collection(ProcessStateCollection.Name)]
public class InternalHealthTests
{
    private const string Secret = "secret-detail-that-must-not-leak";

    private static WebApplicationFactory<Program> Application(int port, HttpMessageHandler mailgun, bool configured = true) =>
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
                services.AddHttpClient(InternalHealthEndpoint.ClientName).ConfigurePrimaryHttpMessageHandler(() => mailgun);
            }));

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return JsonDocument.Parse(body).RootElement;
    }

    [FunctionalFact]
    public async Task GivenMailgunReachable_WhenRequestedOnTheMetricsPort_ThenAnswersHealthy200()
    {
        // Given
        using var factory = Application(MetricsSetup.Port, HealthProbeHandler.Answering(HttpStatusCode.OK));

        // When
        var response = await factory.CreateClient().GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);
        var report = await ReadAsync(response);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", report.GetProperty("status").GetString());
        var service = Assert.Single(report.GetProperty("services").EnumerateArray());
        Assert.Equal("Mailgun", service.GetProperty("name").GetString());
        Assert.Equal("Healthy", service.GetProperty("status").GetString());
        Assert.False(service.TryGetProperty("reason", out _));
    }

    [FunctionalFact]
    public async Task GivenMailgunAnswers500_WhenRequestedOnTheMetricsPort_ThenAnswersDegraded200WithTheHttpReason()
    {
        // Given
        using var factory = Application(MetricsSetup.Port, HealthProbeHandler.Answering(HttpStatusCode.InternalServerError));

        // When
        var response = await factory.CreateClient().GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);
        var report = await ReadAsync(response);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Degraded", report.GetProperty("status").GetString());
        Assert.Equal("Unhealthy", report.GetProperty("services")[0].GetProperty("status").GetString());
        Assert.Equal("HTTP 500", report.GetProperty("services")[0].GetProperty("reason").GetString());
    }

    [FunctionalFact]
    public async Task GivenMailgunRefusesTheConnection_WhenRequestedOnTheMetricsPort_ThenTheReportHoldsOnlyTheReasonWord()
    {
        // Given
        var refusal = new HttpRequestException(
            Secret, new SocketException((int)SocketError.ConnectionRefused), HttpStatusCode.ServiceUnavailable);
        using var factory = Application(MetricsSetup.Port, HealthProbeHandler.Failing(refusal));

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

    [FunctionalFact]
    public async Task GivenMailgunNotConfigured_WhenRequestedOnTheMetricsPort_ThenAnswersHealthyWithoutCallingIt()
    {
        // Given
        var mailgun = HealthProbeHandler.Answering(HttpStatusCode.OK);
        using var factory = Application(MetricsSetup.Port, mailgun, configured: false);

        // When
        var response = await factory.CreateClient().GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);
        var report = await ReadAsync(response);

        // Then
        Assert.Equal("Healthy", report.GetProperty("status").GetString());
        Assert.Equal("NotConfigured", report.GetProperty("services")[0].GetProperty("status").GetString());
        Assert.Equal(0, mailgun.Calls);
    }

    [FunctionalFact]
    public async Task GivenMailgunConfigured_WhenRequestedOnTheMetricsPort_ThenOneBareGetIsMade()
    {
        // Given
        var mailgun = HealthProbeHandler.Answering(HttpStatusCode.OK);
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

    [FunctionalFact]
    public async Task GivenTheRouteInAnotherCase_WhenRequestedOnTheMetricsPort_ThenTheReportIsServed()
    {
        // Given
        using var factory = Application(MetricsSetup.Port, HealthProbeHandler.Answering(HttpStatusCode.OK));

        // When
        var response = await factory.CreateClient().GetAsync("/INTERNAL/Health", TestContext.Current.CancellationToken);
        var report = await ReadAsync(response);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", report.GetProperty("status").GetString());
    }

    [FunctionalFact]
    public async Task GivenTheApplicationPort_WhenRequested_ThenAnswers404WithoutCallingMailgun()
    {
        // Given
        var mailgun = HealthProbeHandler.Answering(HttpStatusCode.OK);
        using var factory = Application(8080, mailgun);

        // When
        var response = await factory.CreateClient().GetAsync(InternalHealthEndpoint.Route, TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, mailgun.Calls);
    }

    [FunctionalFact]
    public async Task GivenAnotherPathOnTheMetricsPort_WhenRequested_ThenAnswers404()
    {
        // Given
        using var factory = Application(MetricsSetup.Port, HealthProbeHandler.Answering(HttpStatusCode.OK));

        // When
        var response = await factory.CreateClient().GetAsync("/internal/other", TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [FunctionalFact]
    public async Task GivenAPostOnTheMetricsPort_WhenRequested_ThenAnswers404WithoutCallingMailgun()
    {
        // Given
        var mailgun = HealthProbeHandler.Answering(HttpStatusCode.OK);
        using var factory = Application(MetricsSetup.Port, mailgun);

        // When
        var response = await factory.CreateClient().PostAsync(InternalHealthEndpoint.Route, null, TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, mailgun.Calls);
    }
}
