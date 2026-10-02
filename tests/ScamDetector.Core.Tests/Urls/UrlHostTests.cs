using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Urls;

public sealed class UrlHostTests
{
    [Theory]
    [InlineData("http://a.example/x", "a.example")]
    [InlineData("https://a.example", "a.example")]
    [InlineData("https://a.example:8080/x?y=1", "a.example")]
    [InlineData("HTTPS://A.EXAMPLE", "a.example")]
    public void Parse_UrlWithScheme_ReturnsLowercaseHost(string url, string expected)
    {
        var host = UrlHost.Parse(url);

        Assert.Equal(expected, host);
    }

    [Theory]
    [InlineData("a.example", "a.example")]
    [InlineData("www.A.example/path", "www.a.example")]
    [InlineData("bit.ly/abc", "bit.ly")]
    public void Parse_UrlWithoutScheme_AddsHttpsAndReturnsHost(string url, string expected)
    {
        var host = UrlHost.Parse(url);

        Assert.Equal(expected, host);
    }

    [Theory]
    [InlineData("a.example.com/redirect?u=http://b", "a.example.com")]
    [InlineData("a.example.com/r?u=https://b.example", "a.example.com")]
    public void Parse_SchemeInsideQueryOfSchemelessUrl_ReturnsOuterHost(string url, string expected)
    {
        var host = UrlHost.Parse(url);

        Assert.Equal(expected, host);
    }

    [Fact]
    public void Parse_Ipv4Host_ReturnsIpAddress()
    {
        var host = UrlHost.Parse("http://192.0.2.1/login");

        Assert.Equal("192.0.2.1", host);
    }

    [Fact]
    public void Parse_InternationalizedHost_ReturnsPunycode()
    {
        var host = UrlHost.Parse("https://tiệm.example");

        Assert.StartsWith("xn--", host);
        Assert.EndsWith(".example", host);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("http://")]
    [InlineData("https://exa mple.com")]
    public void Parse_InvalidUrl_ReturnsNull(string url)
    {
        var host = UrlHost.Parse(url);

        Assert.Null(host);
    }
}
