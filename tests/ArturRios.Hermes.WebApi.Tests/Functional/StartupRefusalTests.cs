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
        // The environment is set, not inherited: WebApiTest leaves the variable at whatever its last test used, and
        // unset it would mean production, which refuses for reasons of its own.
        using var environment = new EnvironmentVariables(
            (EnvironmentVariable, "local"), (InvalidClientVariable, "personal-page"));
        using var console = new ConsoleCapture();

        // When
        var exitCode = await RunMain();

        // Then
        Assert.Equal(1, exitCode);
        Assert.Contains("personal-page", console.Text);
        Assert.Contains("SENDER is required", console.Text);
        Assert.DoesNotContain("is required in production", console.Text);
    }

    [FunctionalFact]
    public async Task GivenProductionWithoutMailgunOrAnAlertAddress_WhenTheApplicationStarts_ThenItExitsNamingThem()
    {
        // Given
        using var environment = new EnvironmentVariables((EnvironmentVariable, "Production"));
        using var console = new ConsoleCapture();

        // When
        var exitCode = await RunMain();

        // Then
        Assert.Equal(1, exitCode);
        Assert.Contains("MAILGUN_API_KEY", console.Text);
        Assert.Contains("MAILGUN_DOMAIN", console.Text);
        Assert.Contains("HERMES_ALERT_TO", console.Text);
    }
}
