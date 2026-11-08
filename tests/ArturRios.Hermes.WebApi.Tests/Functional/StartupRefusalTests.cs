using ArturRios.Util.Test.Attributes;

namespace ArturRios.Hermes.WebApi.Tests.Functional;

[Collection(ProcessStateCollection.Name)]
public class StartupRefusalTests
{
    private const string EnvironmentVariable = "ASPNETCORE_ENVIRONMENT";
    private const string InvalidClientVariable = "HERMES_CLIENTS__0__ID";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>Runs the real entry point. An application that wrongly starts fails the test rather than hanging it.</summary>
    private static Task<int> RunMain() =>
        Task.Run(() => Program.Main([]), TestContext.Current.CancellationToken)
            .WaitAsync(Patience, TestContext.Current.CancellationToken);

    [FunctionalFact]
    public async Task GivenAnInvalidClient_WhenTheApplicationStarts_ThenItExitsNamingTheClient()
    {
        // Given
        using var console = new ConsoleCapture();
        Environment.SetEnvironmentVariable(InvalidClientVariable, "personal-page");

        try
        {
            // When
            var exitCode = await RunMain();

            // Then
            Assert.Equal(1, exitCode);
            Assert.Contains("personal-page", console.Text);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InvalidClientVariable, null);
        }
    }

    [FunctionalFact]
    public async Task GivenProductionWithoutMailgunOrAnAlertAddress_WhenTheApplicationStarts_ThenItExitsNamingThem()
    {
        // Given
        var previous = Environment.GetEnvironmentVariable(EnvironmentVariable);
        using var console = new ConsoleCapture();
        Environment.SetEnvironmentVariable(EnvironmentVariable, "Production");

        try
        {
            // When
            var exitCode = await RunMain();

            // Then
            Assert.Equal(1, exitCode);
            Assert.Contains("MAILGUN_API_KEY", console.Text);
            Assert.Contains("MAILGUN_DOMAIN", console.Text);
            Assert.Contains("HERMES_ALERT_TO", console.Text);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariable, previous);
        }
    }
}
