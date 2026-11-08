using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Attributes;
using ArturRios.Util.Test.Functional;
using Bogus;

namespace ArturRios.Hermes.WebApi.Tests.Functional;

[Collection(ProcessStateCollection.Name)]
public class LogRedactionTests() : WebApiTest<Program>(EnvironmentType.Local)
{
    private readonly string _apiKey = new Faker().Random.AlphaNumeric(40);

    private async Task AssertTheKeyIsNotLoggedWhenRequesting(string route)
    {
        // Given
        using var console = new ConsoleCapture();
        Authorize(_apiKey);

        // When
        await Gateway.GetAsync<object>(route, TestContext.Current.CancellationToken);

        // Then
        await console.WaitForAsync(route);
        Assert.DoesNotContain(_apiKey, console.Text);
    }

    [FunctionalFact]
    public Task GivenAnAuthorizationHeader_WhenAKnownRouteIsLogged_ThenTheKeyIsNotInTheLogs() =>
        AssertTheKeyIsNotLoggedWhenRequesting("/healthcheck");

    [FunctionalFact]
    public Task GivenAnAuthorizationHeader_WhenAnUnknownRouteIsLogged_ThenTheKeyIsNotInTheLogs() =>
        AssertTheKeyIsNotLoggedWhenRequesting("/no-such-route");

    [FunctionalFact]
    public async Task GivenTheHost_WhenItServesARequest_ThenNoLogFileIsCreated()
    {
        // When
        await Gateway.GetAsync<object>("/healthcheck", TestContext.Current.CancellationToken);

        // Then
        Assert.Empty(Directory.GetFiles(AppContext.BaseDirectory, "*.log", SearchOption.AllDirectories));
    }
}
