using ArturRios.Util.Test.Attributes;

namespace ArturRios.Hermes.WebApi.Tests.Functional;

[Collection(ProcessStateCollection.Name)]
public class StartupRefusalTests
{
    private const string InvalidClientVariable = "HERMES_CLIENTS__0__ID";

    [FunctionalFact]
    public void GivenAnInvalidClient_WhenTheApplicationStarts_ThenItExitsNamingTheClient()
    {
        // Given
        using var console = new ConsoleCapture();
        Environment.SetEnvironmentVariable(InvalidClientVariable, "personal-page");

        try
        {
            // When
            var exitCode = Program.Main([]);

            // Then
            Assert.Equal(1, exitCode);
            Assert.Contains("personal-page", console.Text);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InvalidClientVariable, null);
        }
    }
}
