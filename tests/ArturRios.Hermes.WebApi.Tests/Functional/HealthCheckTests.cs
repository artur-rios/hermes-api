using System.Net;
using System.Text.Json;
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

    [FunctionalFact]
    public async Task GivenTheContract_WhenRequestingHealthcheck_ThenTheBodyHoldsOnlyAStatusString()
    {
        // When
        var response = await Gateway.GetAsync<HealthBody>("/healthcheck", TestContext.Current.CancellationToken);

        // Then
        using var body = JsonDocument.Parse(response.RawBody!);
        var property = Assert.Single(body.RootElement.EnumerateObject());
        Assert.Equal("status", property.Name);
        Assert.Equal(JsonValueKind.String, property.Value.ValueKind);
    }

    [FunctionalFact]
    public void GivenTheContainerDefinition_WhenRead_ThenDockerProbesHealthcheck()
    {
        // Given
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
        {
            directory = directory.Parent;
        }

        // When
        var definition = File.ReadAllText(Path.Combine(directory!.FullName, "docker-compose.yml"));

        // Then
        Assert.Contains("healthcheck:", definition);
        Assert.Contains("http://localhost:8080/healthcheck", definition);
    }

    private sealed record HealthBody(string Status);
}
