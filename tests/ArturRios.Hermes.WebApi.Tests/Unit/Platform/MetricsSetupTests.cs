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
        Assert.True(MetricsSetup.IsScrapeRequest(Request(9464, "/metrics")));
    }

    [UnitFact]
    public void GivenTheApiPort_WhenThePathIsMetrics_ThenItIsNotAScrape()
    {
        Assert.False(MetricsSetup.IsScrapeRequest(Request(8080, "/metrics")));
    }

    [UnitFact]
    public void GivenTheMetricsPort_WhenThePathIsNotMetrics_ThenItIsNotAScrape()
    {
        Assert.False(MetricsSetup.IsScrapeRequest(Request(9464, "/healthcheck")));
    }
}
