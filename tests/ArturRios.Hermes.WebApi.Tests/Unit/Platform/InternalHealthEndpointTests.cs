using ArturRios.Hermes.WebApi.Platform;
using ArturRios.Util.Test.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Platform;

public class InternalHealthEndpointTests
{
    private static HttpContext Request(int localPort, string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Connection.LocalPort = localPort;
        context.Request.Method = method;
        context.Request.Path = path;

        return context;
    }

    [UnitTheory]
    [InlineData("/internal/health")]
    [InlineData("/INTERNAL/Health")]
    public void GivenAGetOnTheMetricsPort_WhenThePathIsTheRoute_ThenItIsAHealthRequest(string path)
    {
        // Given
        var request = Request(MetricsSetup.Port, HttpMethods.Get, path);

        // When
        var isHealth = InternalHealthEndpoint.IsHealthRequest(request);

        // Then
        Assert.True(isHealth);
    }

    [UnitTheory]
    [InlineData(8080, "GET", "/internal/health")]
    [InlineData(9464, "POST", "/internal/health")]
    [InlineData(9464, "HEAD", "/internal/health")]
    [InlineData(9464, "GET", "/internal/health/more")]
    [InlineData(9464, "GET", "/metrics")]
    public void GivenAnythingButAGetOfTheRouteOnTheMetricsPort_WhenChecked_ThenItIsNotAHealthRequest(
        int port, string method, string path)
    {
        // Given
        var request = Request(port, method, path);

        // When
        var isHealth = InternalHealthEndpoint.IsHealthRequest(request);

        // Then
        Assert.False(isHealth);
    }

    [UnitFact]
    public void GivenTheRegisteredClient_WhenItsHandlerIsBuilt_ThenItFollowsNoRedirect()
    {
        // Given
        var services = new ServiceCollection();
        InternalHealthEndpoint.Add(services);
        using var provider = services.BuildServiceProvider();

        // When
        HttpMessageHandler handler = provider.GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(InternalHealthEndpoint.ClientName);

        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        // Then
        var primary = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(primary.AllowAutoRedirect);
    }
}
