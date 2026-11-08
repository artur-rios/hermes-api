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
    /// Mounts the scrape endpoint. It is a terminal branch, so this goes first: a scrape reaches nothing else in the
    /// pipeline and is not written to the request log.
    /// </summary>
    public static void Use(IApplicationBuilder app) => app.UseOpenTelemetryPrometheusScrapingEndpoint(IsScrapeRequest);
}
