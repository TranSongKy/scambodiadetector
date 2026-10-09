using System.Net;
using System.Net.Http.Json;
using ScamDetector.Api.Links;
using ScamDetector.Api.Tests.Support;
using ScamDetector.Core.Links;
using ScamDetector.Core.ThreatIntel;
using ScamDetector.Core.Urls;

namespace ScamDetector.Api.Tests.Links;

public sealed class LinkInspectionEndpointsTests : IDisposable
{
    private const string BlockedDomain = "vtp-vandon.online";
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "scamdetector-link-tests-" + Guid.NewGuid().ToString("N"));

    public LinkInspectionEndpointsTests()
    {
        Directory.CreateDirectory(_dataDirectory);
        File.WriteAllText(Path.Combine(_dataDirectory, "blocked_domains.csv"),
            $"domain,source,source_url,first_seen,evidence\n{BlockedDomain},test,https://example.gov.vn,2026-10-01,\n");
        File.WriteAllText(Path.Combine(_dataDirectory, "allowed_domains.csv"), "domain,note\nvietcombank.com.vn,\n");
    }

    [Fact]
    public async Task Inspect_MixedLinksWithoutModel_ReturnsVerdictPerLinkInOrder()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string[] urls = [$"https://{BlockedDomain}/tra-cuu", "https://vietcombank-xacthuc.top/login", "https://bit.ly/abc", "https://vietcombank.com.vn"];

        var response = await client.PostAsJsonAsync(ApiRoutes.LinkInspections, new InspectLinksRequest(urls), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InspectLinksResponse>(CancellationToken.None);
        Assert.NotNull(body);
        Assert.Equal(urls, body.Results.Select(result => result.Url));
        Assert.Equal(
            [LinkVerdictNames.Dangerous, LinkVerdictNames.Dangerous, LinkVerdictNames.Suspicious, LinkVerdictNames.Safe],
            body.Results.Select(result => result.Verdict));
        Assert.Contains(ThreatReasons.BlocklistedDomain, body.Results[0].Reasons);
        Assert.Contains(UrlReasons.BrandImpersonation, body.Results[1].Reasons);
        Assert.Equal([UrlReasons.Shortener], body.Results[2].Reasons);
        Assert.Empty(body.Results[3].Reasons);
    }

    [Fact]
    public async Task Inspect_ObfuscatedBlockedLink_ReturnsDangerous()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.LinkInspections, new InspectLinksRequest(["vtp-vandon[.]online"]), CancellationToken.None);

        var body = await response.Content.ReadFromJsonAsync<InspectLinksResponse>(CancellationToken.None);
        Assert.Equal(LinkVerdictNames.Dangerous, body!.Results.Single().Verdict);
        Assert.Contains(UrlReasons.Obfuscated, body.Results.Single().Reasons);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Inspect_InvalidRequest_ReturnsBadRequestWithErrorCode(string[]? urls, string expectedCode)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.LinkInspections, new InspectLinksRequest(urls), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(CancellationToken.None);
        Assert.Equal(expectedCode, problem!.Code);
    }

    public static TheoryData<string[]?, string> InvalidRequests() => new()
    {
        { null, LinkInspectionErrors.NoLinks.Code },
        { [], LinkInspectionErrors.NoLinks.Code },
        { Enumerable.Repeat("https://a.example", LinkInspectionLimits.MaxLinksPerRequest + 1).ToArray(), LinkInspectionErrors.TooManyLinks.Code },
        { ["https://a.example", " "], LinkInspectionErrors.InvalidLink.Code },
    };

    public void Dispose() => Directory.Delete(_dataDirectory, recursive: true);

    private ScamDetectorApiFactory CreateFactory() =>
        new(settings: new Dictionary<string, string?>(FixtureModelSettings.Missing()) { ["ThreatIntel:DataDirectory"] = _dataDirectory });
}
