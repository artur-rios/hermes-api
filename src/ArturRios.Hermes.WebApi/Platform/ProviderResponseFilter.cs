using Serilog.Core;
using Serilog.Events;

namespace ArturRios.Hermes.WebApi.Platform;

/// <summary>
/// Drops the raw response text ArturRios.Messaging logs when Mailgun refuses a message. Hermes cannot control what a
/// provider puts in an error, and its logs must never hold an address or a subject. The status code stays.
/// </summary>
public sealed class ProviderResponseFilter : ILogEventEnricher
{
    private const string ResponseContentProperty = "ResponseContent";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory) =>
        logEvent.RemovePropertyIfPresent(ResponseContentProperty);
}
