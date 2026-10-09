using ScamDetector.Core.Classification;
using ScamDetector.Core.Links;
using ScamDetector.Core.ThreatIntel;
using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Links;

public sealed class LinkInspectionServiceIntegrationTests
{
    private static readonly LinkInspectionService Service = new(
        new RuleBasedUrlInspector(),
        new ClassificationOptions(),
        new ThreatIntelligenceIndex(["vtp-vandon.online"], [], officialDomains: ["vietcombank.com.vn"]));

    [Theory]
    [InlineData("https://vtp-vandon.online/x", LinkVerdict.Dangerous)]
    [InlineData("vtp-vandon[.]online", LinkVerdict.Dangerous)]
    [InlineData("vietcombank-xacthuc.top", LinkVerdict.Dangerous)]
    [InlineData("https://bit.ly/abc", LinkVerdict.Suspicious)]
    [InlineData("https://vietcombank.com.vn", LinkVerdict.Safe)]
    public async Task InspectAsync_RealInspectorAndIndex_ReturnsExpectedVerdict(string url, LinkVerdict expectedVerdict)
    {
        var result = await Service.InspectAsync([url], CancellationToken.None);

        Assert.Equal(expectedVerdict, result.Value[0].Verdict);
        Assert.Equal(url, result.Value[0].Url);
    }

    [Fact]
    public async Task InspectAsync_BlockedDomain_ListsBlocklistReasonFirst()
    {
        var result = await Service.InspectAsync(["https://vtp-vandon.online/x"], CancellationToken.None);

        Assert.Equal(ThreatReasons.BlocklistedDomain, result.Value[0].Reasons[0]);
    }
}
