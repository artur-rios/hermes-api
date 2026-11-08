using ArturRios.Hermes.WebApi.Platform;
using ArturRios.Util.Test.Attributes;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Platform;

public class MetricsSetupTests
{
    private static HttpContext Request(int localPort, string path)
    {
        var context = new DefaultHttpContext();
        context.Connection.LocalPort = localPort;
        context.Request.Path = path;

        return context;
    }

    [UnitFact]
    public void GivenTheMetricsPort_WhenThePathIsMetrics_ThenItIsAScrape()
    {
        // Given
        var request = Request(9464, "/metrics");

        // When
        var isScrape = MetricsSetup.IsScrapeRequest(request);

        // Then
        Assert.True(isScrape);
    }

    [UnitFact]
    public void GivenTheApiPort_WhenThePathIsMetrics_ThenItIsNotAScrape()
    {
        // Given
        var request = Request(8080, "/metrics");

        // When
        var isScrape = MetricsSetup.IsScrapeRequest(request);

        // Then
        Assert.False(isScrape);
    }

    [UnitFact]
    public void GivenTheMetricsPort_WhenThePathIsNotMetrics_ThenItIsNotAScrape()
    {
        // Given
        var request = Request(9464, "/healthcheck");

        // When
        var isScrape = MetricsSetup.IsScrapeRequest(request);

        // Then
        Assert.False(isScrape);
    }
}
