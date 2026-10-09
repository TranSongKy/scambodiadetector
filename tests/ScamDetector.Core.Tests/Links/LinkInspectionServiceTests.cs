using ScamDetector.Core.Classification;
using ScamDetector.Core.Common;
using ScamDetector.Core.Links;
using ScamDetector.Core.Tests.Fakes;
using ScamDetector.Core.ThreatIntel;
using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Links;

public sealed class LinkInspectionServiceTests
{
    private const string SampleUrl = "https://example.test/a";

    private static readonly UrlFinding ShortenerFinding = new("https://bit.ly/abc", UrlReasons.Shortener);
    private static readonly UrlFinding TldFinding = new("https://x.top", UrlReasons.SuspiciousTopLevelDomain);
    private static readonly UrlFinding BrandFinding = new("https://vietcombank-x.top", UrlReasons.BrandImpersonation);

    private static LinkInspectionService CreateService(
        FakeUrlInspector inspector,
        FakeThreatIntelligence? threat = null,
        double threshold = ClassificationOptions.DefaultScamThreshold) =>
        new(inspector, new ClassificationOptions { ScamThreshold = threshold }, threat);

    private static FakeUrlInspector InspectorReturning(params UrlFinding[] findings) => new(findings);

    private static FakeThreatIntelligence ThreatReturning(ThreatMatch match) => new(match);

    private static string[] Urls(int count) => [.. Enumerable.Range(0, count).Select(index => $"https://a{index}.test")];

    private static Task<Result<IReadOnlyList<LinkInspection>>> Inspect(LinkInspectionService service, params string[] urls) =>
        service.InspectAsync(urls, CancellationToken.None);

    [Fact]
    public async Task InspectAsync_EmptyList_ReturnsNoLinksFailure()
    {
        var inspector = InspectorReturning();
        var service = CreateService(inspector);

        var result = await Inspect(service);

        Assert.True(result.IsFailure);
        Assert.Equal(LinkInspectionErrors.NoLinks, result.Error);
        Assert.Equal(0, inspector.CallCount);
    }

    [Fact]
    public async Task InspectAsync_MaxLinksPerRequest_ReturnsSuccess()
    {
        var service = CreateService(InspectorReturning());

        var result = await Inspect(service, Urls(LinkInspectionLimits.MaxLinksPerRequest));

        Assert.True(result.IsSuccess);
        Assert.Equal(LinkInspectionLimits.MaxLinksPerRequest, result.Value.Count);
    }

    [Fact]
    public async Task InspectAsync_OneOverMaxLinks_ReturnsTooManyLinksFailure()
    {
        var inspector = InspectorReturning();
        var service = CreateService(inspector);

        var result = await Inspect(service, Urls(LinkInspectionLimits.MaxLinksPerRequest + 1));

        Assert.Equal(LinkInspectionErrors.TooManyLinks, result.Error);
        Assert.Equal(0, inspector.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public async Task InspectAsync_BlankLink_ReturnsInvalidLinkFailure(string blankUrl)
    {
        var inspector = InspectorReturning();
        var service = CreateService(inspector);

        var result = await Inspect(service, SampleUrl, blankUrl);

        Assert.Equal(LinkInspectionErrors.InvalidLink, result.Error);
        Assert.Equal(0, inspector.CallCount);
    }

    [Fact]
    public async Task InspectAsync_LinkAtMaxLength_ReturnsSuccess()
    {
        var service = CreateService(InspectorReturning());
        var url = new string('a', LinkInspectionLimits.MaxLinkLength);

        var result = await Inspect(service, url);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task InspectAsync_LinkOverMaxLength_ReturnsInvalidLinkFailure()
    {
        var inspector = InspectorReturning();
        var service = CreateService(inspector);
        var url = new string('a', LinkInspectionLimits.MaxLinkLength + 1);

        var result = await Inspect(service, url);

        Assert.Equal(LinkInspectionErrors.InvalidLink, result.Error);
        Assert.Equal(0, inspector.CallCount);
    }

    [Fact]
    public async Task InspectAsync_MultipleLinks_ReturnsResultsInInputOrder()
    {
        var service = CreateService(InspectorReturning());
        string[] urls = ["https://c.test", "https://a.test", "https://b.test"];

        var result = await Inspect(service, urls);

        Assert.Equal(urls, result.Value.Select(inspection => inspection.Url));
    }

    [Fact]
    public async Task InspectAsync_UrlWithHiddenCharacter_KeepsOriginalUrlAndPassesNormalizedToDependencies()
    {
        var inspector = InspectorReturning();
        var threat = ThreatReturning(ThreatMatch.None);
        var service = CreateService(inspector, threat);
        var originalUrl = "https://exa​mple.test/a";

        var result = await Inspect(service, originalUrl);

        Assert.Equal(originalUrl, result.Value[0].Url);
        Assert.Equal("https://example.test/a", inspector.ReceivedText);
        Assert.Equal("https://example.test/a", threat.ReceivedNormalizedText);
    }

    [Fact]
    public async Task InspectAsync_DecomposedVietnameseUrl_PassesNfcToInspector()
    {
        var inspector = InspectorReturning();
        var service = CreateService(inspector);
        var decomposed = "https://example.test/việt";

        await Inspect(service, decomposed);

        Assert.Equal("https://example.test/việt", inspector.ReceivedText);
    }

    [Fact]
    public async Task InspectAsync_NoFindings_ReturnsSafeWithEmptyReasons()
    {
        var service = CreateService(InspectorReturning());

        var result = await Inspect(service, SampleUrl);

        Assert.Equal(LinkVerdict.Safe, result.Value[0].Verdict);
        Assert.Empty(result.Value[0].Reasons);
    }

    [Fact]
    public async Task InspectAsync_FindingBelowThreshold_ReturnsSuspiciousWithReason()
    {
        var service = CreateService(InspectorReturning(ShortenerFinding));

        var result = await Inspect(service, ShortenerFinding.Url);

        Assert.Equal(LinkVerdict.Suspicious, result.Value[0].Verdict);
        Assert.Equal([UrlReasons.Shortener], result.Value[0].Reasons);
    }

    [Fact]
    public async Task InspectAsync_FindingsCombinedReachThreshold_ReturnsDangerous()
    {
        var service = CreateService(InspectorReturning(BrandFinding, TldFinding));

        var result = await Inspect(service, BrandFinding.Url);

        Assert.Equal(LinkVerdict.Dangerous, result.Value[0].Verdict);
    }

    [Fact]
    public async Task InspectAsync_RiskExactlyAtThreshold_ReturnsDangerous()
    {
        var service = CreateService(InspectorReturning(BrandFinding), threshold: UrlRisk.BrandImpersonationWeight);

        var result = await Inspect(service, BrandFinding.Url);

        Assert.Equal(LinkVerdict.Dangerous, result.Value[0].Verdict);
    }

    [Fact]
    public async Task InspectAsync_BlocklistedDomainWithoutFindings_ReturnsDangerousWithBlocklistReason()
    {
        var threat = ThreatReturning(new ThreatMatch(["bad.test"], null));
        var service = CreateService(InspectorReturning(), threat);

        var result = await Inspect(service, "https://bad.test");

        Assert.Equal(LinkVerdict.Dangerous, result.Value[0].Verdict);
        Assert.Equal([ThreatReasons.BlocklistedDomain], result.Value[0].Reasons);
    }

    [Fact]
    public async Task InspectAsync_BlocklistedDomainWithFindings_PutsBlocklistReasonFirst()
    {
        var threat = ThreatReturning(new ThreatMatch(["bad.test"], null));
        var service = CreateService(InspectorReturning(ShortenerFinding), threat);

        var result = await Inspect(service, "https://bad.test");

        Assert.Equal([ThreatReasons.BlocklistedDomain, UrlReasons.Shortener], result.Value[0].Reasons);
    }

    [Fact]
    public async Task InspectAsync_ThreatBrandImpersonation_CountsTowardRiskAndReasons()
    {
        var threat = ThreatReturning(ThreatMatch.None with { BrandImpersonations = [BrandFinding] });
        var service = CreateService(InspectorReturning(TldFinding), threat);

        var result = await Inspect(service, BrandFinding.Url);

        Assert.Equal(LinkVerdict.Dangerous, result.Value[0].Verdict);
        Assert.Contains(UrlReasons.BrandImpersonation, result.Value[0].Reasons);
        Assert.Contains(UrlReasons.SuspiciousTopLevelDomain, result.Value[0].Reasons);
    }

    [Fact]
    public async Task InspectAsync_DuplicateReasons_ReturnsDistinctReasons()
    {
        var duplicate = new UrlFinding("https://other.test", UrlReasons.Shortener);
        var service = CreateService(InspectorReturning(ShortenerFinding, duplicate));

        var result = await Inspect(service, ShortenerFinding.Url);

        Assert.Equal([UrlReasons.Shortener], result.Value[0].Reasons);
    }

    [Fact]
    public async Task InspectAsync_CancellationToken_IsPassedToUrlInspector()
    {
        var inspector = InspectorReturning();
        var service = CreateService(inspector);
        using var source = new CancellationTokenSource();

        await service.InspectAsync([SampleUrl], source.Token);

        Assert.Equal(source.Token, inspector.ReceivedToken);
    }

    [Fact]
    public async Task InspectAsync_NoThreatIntelligence_UsesFindingsOnly()
    {
        var service = CreateService(InspectorReturning(ShortenerFinding));

        var result = await Inspect(service, ShortenerFinding.Url);

        Assert.Equal(LinkVerdict.Suspicious, result.Value[0].Verdict);
    }
}
