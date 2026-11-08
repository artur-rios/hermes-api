using System.Net;
using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Attributes;
using ArturRios.Util.Test.Functional;

namespace ArturRios.Hermes.WebApi.Tests.Functional;

[Collection(ProcessStateCollection.Name)]
public class HealthCheckTests() : WebApiTest<Program>(EnvironmentType.Local)
{
    [FunctionalFact]
    public async Task GivenNoCredentials_WhenRequestingHealthcheck_ThenAnswers200()
    {
        // When
        var response = await Gateway.GetAsync<HealthBody>("/healthcheck", TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [FunctionalFact]
    public async Task GivenNoCredentials_WhenRequestingHealthcheck_ThenAnswersWithItsStatus()
    {
        // When
        var response = await Gateway.GetAsync<HealthBody>("/healthcheck", TestContext.Current.CancellationToken);

        // Then
        Assert.Equal("healthy", response.Body?.Status);
    }

    private sealed record HealthBody(string Status);
}
