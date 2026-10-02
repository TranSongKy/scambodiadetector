using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Urls;

public sealed class RuleBasedUrlInspectorTests
{
    private readonly RuleBasedUrlInspector _inspector = new();

    [Theory]
    [InlineData("")]
    [InlineData("khong co duong dan")]
    [InlineData("Tài khoản của bạn bị khóa")]
    public async Task InspectAsync_TextWithoutUrl_ReturnsEmpty(string text)
    {
        var findings = await _inspector.InspectAsync(text, CancellationToken.None);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task InspectAsync_SafeLookingUrl_ReturnsEmpty()
    {
        var findings = await _inspector.InspectAsync("xem https://a.example/x", CancellationToken.None);

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("bit.ly/abc")]
    [InlineData("https://tinyurl.com/abc")]
    [InlineData("https://T.CO/abc")]
    public async Task InspectAsync_ShortenerUrl_ReturnsShortenerFinding(string url)
    {
        var findings = await _inspector.InspectAsync($"xem {url}", CancellationToken.None);

        Assert.Equal([new UrlFinding(url, UrlReasons.Shortener)], findings);
    }

    [Fact]
    public async Task InspectAsync_IpAddressHost_ReturnsIpFinding()
    {
        var findings = await _inspector.InspectAsync("vào http://192.0.2.1/login", CancellationToken.None);

        Assert.Equal([new UrlFinding("http://192.0.2.1/login", UrlReasons.IpAddressHost)], findings);
    }

    [Fact]
    public async Task InspectAsync_PunycodeLabel_ReturnsPunycodeFinding()
    {
        var findings = await _inspector.InspectAsync("vào https://xn--fake.example/x", CancellationToken.None);

        Assert.Equal([new UrlFinding("https://xn--fake.example/x", UrlReasons.Punycode)], findings);
    }

    [Fact]
    public async Task InspectAsync_PunycodeInSubdomain_ReturnsPunycodeFinding()
    {
        var findings = await _inspector.InspectAsync("vào https://www.xn--fake.example", CancellationToken.None);

        Assert.Contains(findings, finding => finding.Reason == UrlReasons.Punycode);
    }

    [Theory]
    [InlineData("https://a.example.xyz")]
    [InlineData("https://a.example.top")]
    [InlineData("https://a.example.tk")]
    [InlineData("a-example.vip")]
    public async Task InspectAsync_SuspiciousTopLevelDomain_ReturnsTldFinding(string url)
    {
        var findings = await _inspector.InspectAsync($"vào {url}", CancellationToken.None);

        Assert.Equal([new UrlFinding(url, UrlReasons.SuspiciousTopLevelDomain)], findings);
    }

    [Fact]
    public async Task InspectAsync_ShortenerAndPunycodeAndTld_ReturnsFindingForEachRule()
    {
        var findings = await _inspector.InspectAsync("vào https://xn--fake.example.xyz", CancellationToken.None);

        Assert.Equal(
            [UrlReasons.Punycode, UrlReasons.SuspiciousTopLevelDomain],
            findings.Select(finding => finding.Reason));
    }

    [Fact]
    public async Task InspectAsync_MultipleUrls_ReturnsFindingsForEachUrl()
    {
        var findings = await _inspector.InspectAsync("bit.ly/x và http://192.0.2.1", CancellationToken.None);

        Assert.Equal(
            [
                new UrlFinding("bit.ly/x", UrlReasons.Shortener),
                new UrlFinding("http://192.0.2.1", UrlReasons.IpAddressHost),
            ],
            findings);
    }

    [Fact]
    public async Task InspectAsync_UrlWithTrailingPunctuation_ReportsTrimmedUrl()
    {
        var findings = await _inspector.InspectAsync("vào bit.ly/x.", CancellationToken.None);

        Assert.Equal([new UrlFinding("bit.ly/x", UrlReasons.Shortener)], findings);
    }

    [Fact]
    public async Task InspectAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _inspector.InspectAsync("bit.ly/x", source.Token));
    }
}
