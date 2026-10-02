using System.Net;
using ScamDetector.Api.Tests.Fakes;
using ScamDetector.Api.Tests.Support;

namespace ScamDetector.Api.Tests.HealthChecks;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_ModelLoaded_ReturnsHealthy()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ApiRoutes.Health, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_ModelFilesMissing_ReturnsServiceUnavailable()
    {
        await using var factory = new ScamDetectorApiFactory(settings: FixtureModelSettings.Missing());
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ApiRoutes.Health, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
