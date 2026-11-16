using System.Net;
using ArturRios.Hermes.WebApi.Platform;
using ArturRios.Util.Test.Attributes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;

namespace ArturRios.Hermes.WebApi.Tests.Functional;

/// <summary>
/// Requests as they arrive on the metrics port, which <see cref="ArrivesOnPort"/> fakes. That needs the host builder,
/// which <c>WebApiTest</c> does not expose.
/// </summary>
[Collection(ProcessStateCollection.Name)]
public class MetricsPortTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder
            .UseEnvironment("local")
            .ConfigureTestServices(services => services.AddSingleton<IStartupFilter>(new ArrivesOnPort(MetricsSetup.Port))));

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [FunctionalFact]
    public async Task GivenTheMetricsPort_WhenRequestingMetrics_ThenAnswers200()
    {
        // Given
        var client = _factory.CreateClient();

        // When
        var response = await client.GetAsync("/metrics", TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [FunctionalFact]
    public async Task GivenTheMetricsPort_WhenRequestingAnApiRoute_ThenAnswers404()
    {
        // Given
        var client = _factory.CreateClient();

        // When
        var response = await client.GetAsync(HealthEndpoint.Route, TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
