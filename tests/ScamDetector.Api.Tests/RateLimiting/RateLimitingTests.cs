using System.Net;
using System.Net.Http.Json;
using ScamDetector.Api.Classifications;
using ScamDetector.Api.Tests.Fakes;
using ScamDetector.Api.Tests.Support;

namespace ScamDetector.Api.Tests.RateLimiting;

public sealed class RateLimitingTests
{
    private static readonly Dictionary<string, string?> TwoRequestsPerMinute = new()
    {
        ["RateLimiting:PermitLimit"] = "2",
        ["RateLimiting:WindowSeconds"] = "60",
    };

    [Fact]
    public async Task Classify_MoreRequestsThanPermitLimit_ReturnsTooManyRequests()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0), TwoRequestsPerMinute);
        using var client = factory.CreateClient();
        var request = new ClassifyMessageRequest("Mai hop nhom nhe");

        var first = await client.PostAsJsonAsync(ApiRoutes.Classifications, request, CancellationToken.None);
        var second = await client.PostAsJsonAsync(ApiRoutes.Classifications, request, CancellationToken.None);
        var third = await client.PostAsJsonAsync(ApiRoutes.Classifications, request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task Health_ManyRequests_IsNotRateLimited()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0), TwoRequestsPerMinute);
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 5; attempt++)
            statuses.Add((await client.GetAsync(ApiRoutes.Health, CancellationToken.None)).StatusCode);

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Fact]
    public void Startup_NonPositivePermitLimit_FailsFast()
    {
        var settings = new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = "0" };
        using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0), settings);

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }
}
