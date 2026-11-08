using System.Net;
using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Attributes;
using ArturRios.Util.Test.Functional;

namespace ArturRios.Hermes.WebApi.Tests.Functional;

[Collection(ProcessStateCollection.Name)]
public class MetricsTests() : WebApiTest<Program>(EnvironmentType.Local)
{
    [FunctionalFact]
    public async Task GivenTheApi_WhenRequestingMetricsOffTheMetricsPort_ThenAnswers404()
    {
        // When
        var response = await Gateway.GetAsync<object>("/metrics", TestContext.Current.CancellationToken);

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
