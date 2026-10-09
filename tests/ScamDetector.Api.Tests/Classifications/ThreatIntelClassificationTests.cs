using System.Net;
using System.Net.Http.Json;
using ScamDetector.Api.Classifications;
using ScamDetector.Api.Tests.Fakes;
using ScamDetector.Api.Tests.Support;
using ScamDetector.Core.Classification;
using ScamDetector.Core.ThreatIntel;
using ScamDetector.Core.Urls;

namespace ScamDetector.Api.Tests.Classifications;

public sealed class ThreatIntelClassificationTests : IDisposable
{
    private const string BlockedDomain = "vcb-xacminh.com";
    private const string OfficialDomain = "vietcombank.com.vn";
    private const string TemplateText = "Tài khoản của bạn bị khoá, truy cập <URL> và nhập mã OTP để mở khoá ngay hôm nay";
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "scamdetector-api-tests-" + Guid.NewGuid().ToString("N"));

    public ThreatIntelClassificationTests()
    {
        Directory.CreateDirectory(_dataDirectory);
        File.WriteAllText(Path.Combine(_dataDirectory, "blocked_domains.csv"),
            $"domain,source,source_url,first_seen,evidence\n{BlockedDomain},test,https://example.gov.vn,2026-10-01,\n");
        File.WriteAllText(Path.Combine(_dataDirectory, "scam_templates.csv"),
            $"id,text,source,source_url,first_seen\ntpl_000001,\"{TemplateText}\",test,https://example.gov.vn,2026-10-01\n");
        File.WriteAllText(Path.Combine(_dataDirectory, "allowed_domains.csv"), $"domain,note\n{OfficialDomain},\n");
    }

    [Fact]
    public async Task Classify_BlocklistedDomainWithoutModel_ReturnsScam()
    {
        await using var factory = new ScamDetectorApiFactory(settings: Settings(FixtureModelSettings.Missing()));
        using var client = factory.CreateClient();

        var body = await ClassifyAsync(client, $"Xác minh tài khoản tại https://login.{BlockedDomain}/vcb");

        Assert.Equal(MessageLabelNames.Scam, body.Label);
        Assert.Equal(ThreatMatch.BlocklistedDomainConfidence, body.Confidence);
        Assert.Contains(ThreatReasons.BlocklistedDomain, body.Reasons);
    }

    [Fact]
    public async Task Classify_KnownTemplateWithoutModel_ReturnsScam()
    {
        await using var factory = new ScamDetectorApiFactory(settings: Settings(FixtureModelSettings.Missing()));
        using var client = factory.CreateClient();

        var body = await ClassifyAsync(client, "Tài khoản của bạn bị khoá, truy cập https://abc.xyz và nhập mã OTP để mở khoá ngay hôm nay");

        Assert.Equal(MessageLabelNames.Scam, body.Label);
        Assert.Contains(ThreatReasons.KnownScamTemplate, body.Reasons);
    }

    [Fact]
    public async Task Classify_BlocklistedDomainModelSaysNormal_ReturnsScam()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(0.9, 0.05, 0.05), Settings(new Dictionary<string, string?>()));
        using var client = factory.CreateClient();

        var body = await ClassifyAsync(client, $"Xem ảnh tại {BlockedDomain}/anh");

        Assert.Equal(MessageLabelNames.Scam, body.Label);
        Assert.Contains(ThreatReasons.BlocklistedDomain, body.Reasons);
    }

    [Fact]
    public async Task Classify_BrandImpersonationOnSuspiciousTldWithoutModel_ReturnsScam()
    {
        await using var factory = new ScamDetectorApiFactory(settings: Settings(FixtureModelSettings.Missing()));
        using var client = factory.CreateClient();

        var body = await ClassifyAsync(client, "Tài khoản bị khoá, xác thực tại https://vietcombank-xacthuc.top/login");

        Assert.Equal(MessageLabelNames.Scam, body.Label);
        Assert.Contains(UrlReasons.BrandImpersonation, body.Reasons);
        Assert.Contains(UrlReasons.SuspiciousTopLevelDomain, body.Reasons);
    }

    [Fact]
    public async Task Classify_ObfuscatedBlocklistedLinkWithoutModel_ReturnsScam()
    {
        await using var factory = new ScamDetectorApiFactory(settings: Settings(FixtureModelSettings.Missing()));
        using var client = factory.CreateClient();

        var body = await ClassifyAsync(client, "Tra cứu đơn hàng tại vcb-xacminh[.]com ngay");

        Assert.Equal(MessageLabelNames.Scam, body.Label);
        Assert.Contains(ThreatReasons.BlocklistedDomain, body.Reasons);
        Assert.Contains(UrlReasons.Obfuscated, body.Reasons);
    }

    [Fact]
    public async Task Classify_OfficialBrandLinkWithoutModel_IsNotFlaggedAndReturnsServiceUnavailable()
    {
        await using var factory = new ScamDetectorApiFactory(settings: Settings(FixtureModelSettings.Missing()));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            ApiRoutes.Classifications,
            new ClassifyMessageRequest("Đăng nhập https://vcbdigibank.vietcombank.com.vn để xem sao kê"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Classify_BrandImpersonationWithModelSayingNormal_RaisesScamProbability()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(0.5, 0.0, 0.5), Settings(new Dictionary<string, string?>()));
        using var client = factory.CreateClient();

        var body = await ClassifyAsync(client, "Nhận quà tri ân tại vietcombank-uudai.com nhé");

        Assert.Equal(MessageLabelNames.Scam, body.Label);
        Assert.Equal(UrlRisk.CombineWithModel(0.5, UrlRisk.BrandImpersonationWeight), body.Confidence, precision: 10);
        Assert.DoesNotContain(ClassificationReasons.ModelPredictedScam, body.Reasons);
        Assert.Contains(UrlReasons.BrandImpersonation, body.Reasons);
    }

    [Fact]
    public async Task Classify_NoThreatSignalWithoutModel_ReturnsServiceUnavailable()
    {
        await using var factory = new ScamDetectorApiFactory(settings: Settings(FixtureModelSettings.Missing()));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Classifications, new ClassifyMessageRequest("Mai 7h họp nhóm nha"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    public void Dispose() => Directory.Delete(_dataDirectory, recursive: true);

    private Dictionary<string, string?> Settings(IReadOnlyDictionary<string, string?> baseSettings) =>
        new(baseSettings) { ["ThreatIntel:DataDirectory"] = _dataDirectory };

    private static async Task<ClassificationResponse> ClassifyAsync(HttpClient client, string text)
    {
        var response = await client.PostAsJsonAsync(ApiRoutes.Classifications, new ClassifyMessageRequest(text), CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClassificationResponse>(CancellationToken.None);
        Assert.NotNull(body);
        return body;
    }
}
