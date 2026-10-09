using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Urls;

public sealed class UrlRiskTests
{
    private const string Url = "http://a.example";
    private const string UnknownReason = "lookalike_domain";

    [Theory]
    [InlineData(UrlReasons.BrandImpersonation, UrlRisk.BrandImpersonationWeight)]
    [InlineData(UrlReasons.Obfuscated, UrlRisk.ObfuscatedWeight)]
    [InlineData(UrlReasons.IpAddressHost, UrlRisk.IpAddressHostWeight)]
    [InlineData(UrlReasons.Punycode, UrlRisk.PunycodeWeight)]
    [InlineData(UrlReasons.SuspiciousTopLevelDomain, UrlRisk.SuspiciousTopLevelDomainWeight)]
    [InlineData(UrlReasons.Shortener, UrlRisk.ShortenerWeight)]
    public void WeightOf_KnownReason_ReturnsConfiguredWeight(string reason, double expected)
    {
        var weight = UrlRisk.WeightOf(reason);

        Assert.Equal(expected, weight);
    }

    [Theory]
    [InlineData(UnknownReason)]
    [InlineData("")]
    [InlineData("URL_SHORTENER")]
    public void WeightOf_UnknownReason_ReturnsZero(string reason)
    {
        var weight = UrlRisk.WeightOf(reason);

        Assert.Equal(0, weight);
    }

    [Fact]
    public void Combine_NoFindings_ReturnsZero()
    {
        var risk = UrlRisk.Combine([]);

        Assert.Equal(0, risk);
    }

    [Fact]
    public void Combine_SingleFinding_ReturnsItsWeight()
    {
        var risk = UrlRisk.Combine([new UrlFinding(Url, UrlReasons.BrandImpersonation)]);

        Assert.Equal(UrlRisk.BrandImpersonationWeight, risk, precision: 10);
    }

    [Fact]
    public void Combine_TwoDifferentReasons_ReturnsNoisyOr()
    {
        var findings = new[]
        {
            new UrlFinding(Url, UrlReasons.BrandImpersonation),
            new UrlFinding(Url, UrlReasons.Obfuscated),
        };

        var risk = UrlRisk.Combine(findings);

        Assert.Equal(1 - (1 - 0.6) * (1 - 0.4), risk, precision: 10);
    }

    [Fact]
    public void Combine_SameReasonOnDifferentUrls_CountsReasonOnce()
    {
        var findings = new[]
        {
            new UrlFinding("http://a.example", UrlReasons.Shortener),
            new UrlFinding("http://b.example", UrlReasons.Shortener),
        };

        var risk = UrlRisk.Combine(findings);

        Assert.Equal(UrlRisk.ShortenerWeight, risk, precision: 10);
    }

    [Fact]
    public void Combine_OnlyUnknownReasons_ReturnsZero()
    {
        var risk = UrlRisk.Combine([new UrlFinding(Url, UnknownReason)]);

        Assert.Equal(0, risk);
    }

    [Fact]
    public void Combine_AllKnownReasons_StaysBelowOne()
    {
        var findings = new[]
        {
            new UrlFinding(Url, UrlReasons.BrandImpersonation),
            new UrlFinding(Url, UrlReasons.Obfuscated),
            new UrlFinding(Url, UrlReasons.IpAddressHost),
            new UrlFinding(Url, UrlReasons.Punycode),
            new UrlFinding(Url, UrlReasons.SuspiciousTopLevelDomain),
            new UrlFinding(Url, UrlReasons.Shortener),
        };

        var risk = UrlRisk.Combine(findings);

        Assert.InRange(risk, 0.9, 1);
    }

    [Theory]
    [InlineData(0.5, 0.6, 0.8)]
    [InlineData(0, 0, 0)]
    [InlineData(0.3, 0, 0.3)]
    [InlineData(0, 0.4, 0.4)]
    [InlineData(1, 0.4, 1)]
    [InlineData(0.3, 1, 1)]
    public void CombineWithModel_Probabilities_ReturnsNoisyOr(double modelProbability, double urlRisk, double expected)
    {
        var combined = UrlRisk.CombineWithModel(modelProbability, urlRisk);

        Assert.Equal(expected, combined, precision: 10);
    }
}
