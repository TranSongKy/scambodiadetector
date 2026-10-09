using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Urls;

public sealed class UrlExtractorTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("khong co duong dan")]
    [InlineData("Tài khoản của bạn bị khóa")]
    public void Extract_TextWithoutUrl_ReturnsEmpty(string text)
    {
        var urls = UrlExtractor.Extract(text);

        Assert.Empty(urls);
    }

    [Theory]
    [InlineData("xem http://a.example/x ngay", "http://a.example/x")]
    [InlineData("xem https://a.example ngay", "https://a.example")]
    [InlineData("xem www.a.example ngay", "www.a.example")]
    [InlineData("xem fake-shop.vn ngay", "fake-shop.vn")]
    [InlineData("xem bit.ly/abc ngay", "bit.ly/abc")]
    [InlineData("xem HTTPS://A.EXAMPLE ngay", "HTTPS://A.EXAMPLE")]
    public void Extract_TextWithUrl_ReturnsUrl(string text, string expected)
    {
        var urls = UrlExtractor.Extract(text);

        Assert.Equal([expected], urls);
    }

    [Theory]
    [InlineData("vào http://a.example.", "http://a.example")]
    [InlineData("vào http://a.example!?", "http://a.example")]
    [InlineData("(http://a.example)", "http://a.example")]
    [InlineData("\"http://a.example\"", "http://a.example")]
    [InlineData("vào http://a.example/x,", "http://a.example/x")]
    public void Extract_UrlWithTrailingPunctuation_TrimsPunctuation(string text, string expected)
    {
        var urls = UrlExtractor.Extract(text);

        Assert.Equal([expected], urls);
    }

    [Fact]
    public void Extract_MultipleUrls_ReturnsAllInOrder()
    {
        var urls = UrlExtractor.Extract("một http://a.example hai bit.ly/x ba www.b.example/y");

        Assert.Equal(["http://a.example", "bit.ly/x", "www.b.example/y"], urls);
    }

    [Fact]
    public void Extract_UnknownTopLevelDomainWithoutScheme_ReturnsEmpty()
    {
        var urls = UrlExtractor.Extract("file readme.txt");

        Assert.Empty(urls);
    }

    [Fact]
    public void Extract_UrlWithVietnameseTextAround_ReturnsOnlyUrl()
    {
        var urls = UrlExtractor.Extract("Nhấn vào http://a.example để nhận quà");

        Assert.Equal(["http://a.example"], urls);
    }

    [Fact]
    public void Replace_TextWithUrl_ReplacesUrl()
    {
        var replaced = UrlExtractor.Replace("xem http://a.example/x ngay", "<URL>");

        Assert.Equal("xem <URL> ngay", replaced);
    }

    [Theory]
    [InlineData("vào http://a.example.", "vào <URL>.")]
    [InlineData("(http://a.example)", "(<URL>)")]
    [InlineData("vào http://a.example!?", "vào <URL>!?")]
    public void Replace_UrlWithTrailingPunctuation_PreservesPunctuation(string text, string expected)
    {
        var replaced = UrlExtractor.Replace(text, "<URL>");

        Assert.Equal(expected, replaced);
    }

    [Fact]
    public void Replace_MultipleUrls_ReplacesAll()
    {
        var replaced = UrlExtractor.Replace("a.example.com và bit.ly/x", "<URL>");

        Assert.Equal("<URL> và <URL>", replaced);
    }

    [Fact]
    public void Replace_TextWithoutUrl_ReturnsSameText()
    {
        var replaced = UrlExtractor.Replace("Tai khoan cua ban", "<URL>");

        Assert.Equal("Tai khoan cua ban", replaced);
    }

    [Fact]
    public void Replace_EmptyText_ReturnsEmpty()
    {
        var replaced = UrlExtractor.Replace(string.Empty, "<URL>");

        Assert.Equal(string.Empty, replaced);
    }

    [Theory]
    [InlineData("xem vtp-vandon[.]online ngay", "vtp-vandon.online")]
    [InlineData("xem vtp-vandon(.)online ngay", "vtp-vandon.online")]
    [InlineData("xem vtp-vandon [dot] online ngay", "vtp-vandon.online")]
    [InlineData("xem vtp-vandon(chấm)online ngay", "vtp-vandon.online")]
    [InlineData("xem hxxps://evil.example.com/x ngay", "https://evil.example.com/x")]
    [InlineData("xem hxxp://evil.example.com ngay", "http://evil.example.com")]
    [InlineData("xem hxxps[:]//evil.example.com/x ngay", "https://evil.example.com/x")]
    [InlineData("xem h**ps://evil.example.com/x ngay", "https://evil.example.com/x")]
    public void Extract_ObfuscatedLink_ReturnsRestoredUrl(string text, string expected)
    {
        var urls = UrlExtractor.Extract(text);

        Assert.Equal([expected], urls);
    }

    [Fact]
    public void ExtractLiteral_BracketedDotLink_ReturnsEmpty()
    {
        var urls = UrlExtractor.ExtractLiteral("xem vtp-vandon[.]online ngay");

        Assert.Empty(urls);
    }

    [Fact]
    public void ExtractLiteral_DefangedScheme_DoesNotRestoreScheme()
    {
        var urls = UrlExtractor.ExtractLiteral("xem hxxps://evil.example.com/x ngay");

        Assert.DoesNotContain(urls, url => url.StartsWith("https://", StringComparison.Ordinal));
    }

    [Fact]
    public void ExtractLiteral_PlainUrl_ReturnsUrl()
    {
        var urls = UrlExtractor.ExtractLiteral("xem https://a.example/x ngay");

        Assert.Equal(["https://a.example/x"], urls);
    }

    [Fact]
    public void Extract_OrdinaryBracketedText_ReturnsEmpty()
    {
        var urls = UrlExtractor.Extract("[Thông báo] Gặp nhau (chiều nay) nhé");

        Assert.Empty(urls);
    }
}
