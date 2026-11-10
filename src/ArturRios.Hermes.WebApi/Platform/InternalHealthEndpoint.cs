using System.Text.Json;
using System.Text.Json.Serialization;
using ArturRios.Hermes.WebApi.Emails;

namespace ArturRios.Hermes.WebApi.Platform;

/// <summary>
/// The detailed health report, served only on the metrics port. Traefik routes the application port alone and nothing
/// publishes the metrics port, so yggdrasil's status API reads it without a credential. For the same reason it carries
/// only names, statuses and reason words.
/// </summary>
public static class InternalHealthEndpoint
{
    public const string Route = "/internal/health";
    public const string ClientName = "mailgun-health";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private const string MailgunAddress = "https://api.mailgun.net/";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static bool IsHealthRequest(HttpContext context) =>
        context.Connection.LocalPort == MetricsSetup.Port
        && HttpMethods.IsGet(context.Request.Method)
        && context.Request.Path.Equals(Route, StringComparison.OrdinalIgnoreCase);

    public static void Add(IServiceCollection services) =>
        // Redirects stay off so the check asks one fixed host and follows no one else's answer.
        services.AddHttpClient(ClientName).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
        });

    public static async Task Handle(HttpContext context)
    {
        var mailgun = await CheckMailgunAsync(context.RequestServices, context.RequestAborted);

        // Mailgun is not required: Hermes still logs, and owners still get answers, without it.
        var status = mailgun.Status == Statuses.Unhealthy ? Statuses.Degraded : Statuses.Healthy;

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";

        await JsonSerializer.SerializeAsync(
            context.Response.Body, new Report(status, [mailgun]), Json, context.RequestAborted);
    }

    /// <summary>Reachability only: any HTTP answer counts, and the call carries no key and uses no quota.</summary>
    private static async Task<Service> CheckMailgunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!services.GetRequiredService<MailgunSettings>().IsConfigured)
        {
            return new Service("Mailgun", Statuses.NotConfigured);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            var client = services.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);

            // Headers only, then dispose: the body is never read.
            using var response = await client.GetAsync(MailgunAddress, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            return new Service("Mailgun", Statuses.Healthy);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InternalHealthEndpoint).FullName!)
                .LogWarning(exception, "Mailgun could not be reached by the health check");

            return new Service("Mailgun", Statuses.Unhealthy, HealthReason.From(exception));
        }
    }

    private static class Statuses
    {
        public const string Healthy = "Healthy";
        public const string Degraded = "Degraded";
        public const string Unhealthy = "Unhealthy";
        public const string NotConfigured = "NotConfigured";
    }

    private sealed record Report(string Status, IReadOnlyList<Service> Services);

    private sealed record Service(string Name, string Status, string? Reason = null);
}
