using ArturRios.Hermes.WebApi.Platform;
using ArturRios.Util.Test.Attributes;
using Serilog.Events;
using Serilog.Parsing;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Platform;

public class ProviderResponseFilterTests
{
    private static LogEvent Event(params (string Name, string Value)[] properties) => new(
        DateTimeOffset.UnixEpoch,
        LogEventLevel.Warning,
        null,
        new MessageTemplateParser().Parse("Mailgun rejected the message with {StatusCode}"),
        properties.Select(property => new LogEventProperty(property.Name, new ScalarValue(property.Value))));

    [UnitFact]
    public void GivenAnEventWithTheProvidersResponse_WhenEnriched_ThenTheResponseIsDropped()
    {
        // Given
        var logEvent = Event(("StatusCode", "400"), ("ResponseContent", "'to' owner@example.com rejected"));

        // When
        new ProviderResponseFilter().Enrich(logEvent, null!);

        // Then
        Assert.False(logEvent.Properties.ContainsKey("ResponseContent"));
        Assert.Equal("\"400\"", logEvent.Properties["StatusCode"].ToString());
    }

    [UnitFact]
    public void GivenAnEventWithoutTheProvidersResponse_WhenEnriched_ThenItKeepsEveryProperty()
    {
        // Given
        var logEvent = Event(("ClientId", "personal-page"), ("RecipientRef", "abc123"));

        // When
        new ProviderResponseFilter().Enrich(logEvent, null!);

        // Then
        Assert.Equal(["ClientId", "RecipientRef"], logEvent.Properties.Keys.Order());
    }
}
