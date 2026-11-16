using ArturRios.Util.Test.Attributes;

namespace ArturRios.Hermes.WebApi.Tests.Unit.Platform;

[Collection(ProcessStateCollection.Name)]
public class ConfigurationTests
{
    private const string Variable = "HERMES_ALERT_TO";

    [UnitFact]
    public void GivenAnEnvironmentVariable_WhenTheBuilderIsCreated_ThenConfigurationHasIt()
    {
        // Given
        using var environment = new EnvironmentVariables((Variable, "owner@example.com"));

        // When
        var builder = Program.CreateBuilder([]);

        // Then
        Assert.Equal("owner@example.com", builder.Configuration[Variable]);
    }

    [UnitFact]
    public void GivenTheBuiltApplication_WhenItsFilesAreListed_ThenItShipsNoSettingsFile()
    {
        // When
        var settings = Directory.GetFiles(AppContext.BaseDirectory, "appsettings*.json", SearchOption.AllDirectories);

        // Then
        Assert.Empty(settings);
    }
}
