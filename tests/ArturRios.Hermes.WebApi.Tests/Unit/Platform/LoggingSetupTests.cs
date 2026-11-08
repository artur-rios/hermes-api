using System.Text.Json;
using ArturRios.Hermes.WebApi.Platform;
using ArturRios.Util.Test.Attributes;
using Serilog;
using Serilog.Core;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Platform;

[Collection(ProcessStateCollection.Name)]
public class LoggingSetupTests
{
    private static Logger CreateLogger() => LoggingSetup.Configure(new LoggerConfiguration()).CreateLogger();

    [UnitFact]
    public void GivenTheLogger_WhenAnEventIsWritten_ThenTheConsoleOutputIsJson()
    {
        // Given
        using var console = new ConsoleCapture();
        using var logger = CreateLogger();

        // When
        logger.Information("Sent to {RecipientRef}", "abc123");

        // Then
        var line = console.Text.Trim();
        using var document = JsonDocument.Parse(line);
        Assert.Equal("abc123", document.RootElement.GetProperty("Properties").GetProperty("RecipientRef").GetString());
    }

    [UnitFact]
    public void GivenTheLogger_WhenAFrameworkInformationEventIsWritten_ThenItIsNotLogged()
    {
        // Given
        using var console = new ConsoleCapture();
        using var logger = CreateLogger();

        // When
        logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Hosting.Diagnostics")
            .Information("Request starting");

        // Then
        Assert.Empty(console.Text);
    }
}
