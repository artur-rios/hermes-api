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
        Environment.SetEnvironmentVariable(Variable, "owner@example.com");

        try
        {
            // When
            var builder = Program.CreateBuilder([]);

            // Then
            Assert.Equal("owner@example.com", builder.Configuration[Variable]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, null);
        }
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
