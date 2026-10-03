using System.Net;
using System.Net.Http.Json;
using ScamDetector.Api.Classifications;
using ScamDetector.Api.Tests.Fakes;
using ScamDetector.Api.Tests.Support;
using ScamDetector.Core.Classification;
using ScamDetector.Core.ThreatIntel;

namespace ScamDetector.Api.Tests.Classifications;

public sealed class ThreatIntelClassificationTests : IDisposable
{
    private const string BlockedDomain = "vcb-xacminh.com";
    private const string TemplateText = "Tài khoản của bạn bị khoá, truy cập <URL> và nhập mã OTP để mở khoá ngay hôm nay";
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "scamdetector-api-tests-" + Guid.NewGuid().ToString("N"));

    public ThreatIntelClassificationTests()
    {
        Directory.CreateDirectory(_dataDirectory);
        File.WriteAllText(Path.Combine(_dataDirectory, "blocked_domains.csv"),
            $"domain,source,source_url,first_seen,evidence\n{BlockedDomain},test,https://example.gov.vn,2026-10-01,\n");
        File.WriteAllText(Path.Combine(_dataDirectory, "scam_templates.csv"),
            $"id,text,source,source_url,first_seen\ntpl_000001,\"{TemplateText}\",test,https://example.gov.vn,2026-10-01\n");
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
