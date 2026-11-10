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
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(5);

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
        services.AddSingleton<MailgunHealth>()
            .AddHttpClient(ClientName).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
            });

    public static async Task Handle(HttpContext context)
    {
        var mailgun = await context.RequestServices.GetRequiredService<MailgunHealth>().GetAsync(context.RequestAborted);

        // Mailgun is not required: Hermes still logs, and owners still get answers, without it.
        var status = mailgun.Status == Statuses.Unhealthy ? Statuses.Degraded : Statuses.Healthy;

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";

        await JsonSerializer.SerializeAsync(
            context.Response.Body, new Report(status, [mailgun]), Json, context.RequestAborted);
    }

    /// <summary>
    /// The Mailgun check, shared by every caller. The endpoint has no credential, so any internal container can call it
    /// in a loop: the result is kept for <see cref="CacheFor"/> from when the check finished, and callers who arrive
    /// during a check wait for it instead of starting their own. The check runs on its own timeout, not on any one
    /// request's, so a caller who gives up does not cancel it for the rest.
    /// </summary>
    public sealed class MailgunHealth(
        MailgunSettings settings, IHttpClientFactory clients, TimeProvider clock, ILogger<MailgunHealth> logger)
    {
        private readonly object _gate = new();

        private Task<Service>? _inFlight;
        private Service? _last;
        private DateTimeOffset _finishedAt;
        private string? _logged;

        internal async Task<Service> GetAsync(CancellationToken cancellationToken)
        {
            Task<Service> check;

            lock (_gate)
            {
                if (_inFlight is null && _last is not null && clock.GetUtcNow() - _finishedAt < CacheFor)
                {
                    return _last;
                }

                check = _inFlight ??= Task.Run(CheckAsync, CancellationToken.None);
            }

            return await check.WaitAsync(cancellationToken);
        }

        private async Task<Service> CheckAsync()
        {
            Service result;

            try
            {
                result = await ProbeAsync();
            }
            catch (Exception exception)
            {
                result = new Service("Mailgun", Statuses.Unhealthy, HealthReason.From(exception));
            }

            lock (_gate)
            {
                _last = result;
                _finishedAt = clock.GetUtcNow();
                _inFlight = null;
            }

            // Once per change, and only the reason word: a flood of requests cannot flood the log.
            var summary = $"{result.Status} {result.Reason}";

            if (summary != _logged)
            {
                _logged = summary;

                if (result.Status == Statuses.Unhealthy)
                {
                    logger.LogWarning("Mailgun could not be reached by the health check: {Reason}", result.Reason);
                }
            }

            return result;
        }

        /// <summary>Reachability only: any HTTP answer counts, and the call carries no key and uses no quota.</summary>
        private async Task<Service> ProbeAsync()
        {
            if (!settings.IsConfigured)
            {
                return new Service("Mailgun", Statuses.NotConfigured);
            }

            using var timeout = new CancellationTokenSource(Timeout);

            // Headers only, then dispose: the body is never read.
            using var response = await clients.CreateClient(ClientName)
                .GetAsync(MailgunAddress, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            return new Service("Mailgun", Statuses.Healthy);
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

    internal sealed record Service(string Name, string Status, string? Reason = null);
}
