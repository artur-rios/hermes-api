using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace ArturRios.Hermes.WebApi.Platform;

/// <summary>
/// Prometheus metrics on their own port. Kestrel listens on both ports and serves one pipeline, so a scrape is told
/// apart by the port the connection arrived on, which a caller cannot influence. The port is reachable only over the
/// telemetry network, because nothing publishes it.
/// </summary>
public static class MetricsSetup
{
    public const int Port = 9464;

    private const string ScrapePath = "/metrics";
    private const string ServiceName = "hermes-api";

    public static bool IsScrapeRequest(HttpContext context) =>
        context.Connection.LocalPort == Port
        && context.Request.Path.Equals(ScrapePath, StringComparison.OrdinalIgnoreCase);

    public static void Add(IServiceCollection services) =>
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddPrometheusExporter());

    /// <summary>
    /// Mounts the scrape endpoint and the internal health report, and closes the rest of the metrics port. All are
    /// terminal branches, so this goes first: a scrape is not written to the request log, and the telemetry network
    /// cannot reach the API.
    /// </summary>
    public static void Use(IApplicationBuilder app)
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint(IsScrapeRequest);

        app.MapWhen(InternalHealthEndpoint.IsHealthRequest, health => health.Run(InternalHealthEndpoint.Handle));

        app.MapWhen(
            context => context.Connection.LocalPort == Port,
            metricsPort => metricsPort.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;

                return Task.CompletedTask;
            }));
    }
}
