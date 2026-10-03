using System.Text;
using ScamDetector.Core.Text;
using ScamDetector.Core.ThreatIntel;

namespace ScamDetector.Core.Tests.ThreatIntel;

public sealed class ThreatIntelligenceIndexTests
{
    private const string BlockedDomain = "bad.example";

    private static ThreatMatch MatchText(ThreatIntelligenceIndex index, string text) =>
        index.Match(text, ModelInputMasker.Mask(text));

    private static ThreatIntelligenceIndex DomainIndex() => new([BlockedDomain], []);

    private static ThreatIntelligenceIndex TemplateIndex(params ScamTemplateEntry[] templates) => new([], templates);

    [Fact]
    public void Match_ExactBlockedDomain_ReturnsDomain()
    {
        var match = MatchText(DomainIndex(), "Nhấn http://bad.example/x để nhận quà");

        Assert.Equal([BlockedDomain], match.BlocklistedDomains);
        Assert.True(match.IsStrongSignal);
    }

    [Fact]
    public void Match_Subdomain_ReturnsSubdomainHost()
    {
        var match = MatchText(DomainIndex(), "Nhấn https://a.b.bad.example/x");

        Assert.Equal(["a.b.bad.example"], match.BlocklistedDomains);
    }

    [Fact]
    public void Match_ParentOfBlockedDomain_ReturnsNoMatch()
    {
        var index = new ThreatIntelligenceIndex(["sub.bad.example"], []);

        var match = MatchText(index, "Nhấn http://bad.example/x");

        Assert.False(match.HasBlocklistedDomain);
    }

    [Fact]
    public void Match_UnrelatedDomainWithSameSuffixText_ReturnsNoMatch()
    {
        var match = MatchText(DomainIndex(), "Nhấn http://notbad.example/x");

        Assert.False(match.HasBlocklistedDomain);
    }

    [Fact]
    public void Match_TopLevelDomainOnlyEntry_NeverMatches()
    {
        var index = new ThreatIntelligenceIndex(["example"], []);

        var match = MatchText(index, "Nhấn http://bad.example/x");

        Assert.False(match.HasBlocklistedDomain);
    }

    [Theory]
    [InlineData("http://bad.example")]
    [InlineData("https://bad.example")]
    [InlineData("http://www.bad.example")]
    [InlineData("https://WWW.BAD.EXAMPLE/Path")]
    [InlineData("HTTP://Bad.Example")]
    public void Match_UrlVariants_ReturnsBlockedDomain(string url)
    {
        var match = MatchText(DomainIndex(), $"Nhấn {url} ngay");

        Assert.True(match.HasBlocklistedDomain);
    }

    [Fact]
    public void Match_BlockedDomainEntryWithUpperCaseAndSpaces_IsNormalized()
    {
        var index = new ThreatIntelligenceIndex(["  BAD.Example "], []);

        var match = MatchText(index, "Nhấn http://bad.example/x");

        Assert.True(match.HasBlocklistedDomain);
    }

    [Fact]
    public void Match_SameDomainTwice_ReturnsDistinctDomains()
    {
        var match = MatchText(DomainIndex(), "http://bad.example/a và http://bad.example/b");

        Assert.Single(match.BlocklistedDomains);
    }

    [Fact]
    public void Match_TextWithoutUrl_ReturnsNoDomain()
    {
        var match = MatchText(DomainIndex(), "Tai khoan cua ban bi khoa");

        Assert.False(match.HasBlocklistedDomain);
    }

    [Fact]
    public void Match_TemplateCoverageAtThreshold_ReturnsTemplate()
    {
        var index = new ThreatIntelligenceIndex([], [new ScamTemplateEntry("t1", "a b c d e f")], minimumCoverage: 0.75);

        var match = MatchText(index, "a b c d e x");

        Assert.Equal("t1", match.Template?.TemplateId);
        Assert.Equal(0.75, match.Template?.Coverage);
    }

    [Fact]
    public void Match_TemplateCoverageAboveThreshold_ReturnsTemplate()
    {
        var index = TemplateIndex(new ScamTemplateEntry("t1", "a b c d e"));

        var match = MatchText(index, "a b c d e");

        Assert.Equal(new TemplateMatch("t1", 1.0), match.Template);
        Assert.True(match.IsStrongSignal);
    }

    [Fact]
    public void Match_TemplateCoverageBelowThreshold_ReturnsNull()
    {
        var index = new ThreatIntelligenceIndex([], [new ScamTemplateEntry("t1", "a b c d e f")], minimumCoverage: 0.75);

        var match = MatchText(index, "a b c d x y");

        Assert.Null(match.Template);
        Assert.False(match.IsStrongSignal);
    }

    [Fact]
    public void Match_DefaultThresholdJustBelow_ReturnsNull()
    {
        var index = TemplateIndex(new ScamTemplateEntry("t1", "a b c d e f g h i j k"));

        var match = MatchText(index, "a b c d e f g x y z w");

        Assert.Null(match.Template);
    }

    [Fact]
    public void Match_MultipleTemplatesAboveThreshold_ReturnsHighestCoverage()
    {
        var index = TemplateIndex(
            new ScamTemplateEntry("low", "a b c d e f"),
            new ScamTemplateEntry("high", "a b c d e"));

        var match = MatchText(index, "a b c d e");

        Assert.Equal("high", match.Template?.TemplateId);
    }

    [Fact]
    public void Match_TiedCoverage_ReturnsSmallestTemplateId()
    {
        var index = TemplateIndex(
            new ScamTemplateEntry("b", "a b c d"),
            new ScamTemplateEntry("a", "a b c d"));

        var match = MatchText(index, "a b c d");

        Assert.Equal("a", match.Template?.TemplateId);
    }

    [Fact]
    public void Match_TemplateWithUrlPlaceholder_MatchesMaskedMessage()
    {
        var index = TemplateIndex(new ScamTemplateEntry("t1", "Nhấn <URL> để nhận quà"));

        var match = MatchText(index, "Nhấn http://other.example/x để nhận quà");

        Assert.Equal("t1", match.Template?.TemplateId);
    }

    [Fact]
    public void Match_DifferentCaseMessage_MatchesTemplate()
    {
        var index = TemplateIndex(new ScamTemplateEntry("t1", "tài khoản của bạn bị khóa"));

        var match = MatchText(index, "TÀI KHOẢN CỦA BẠN BỊ KHÓA");

        Assert.Equal("t1", match.Template?.TemplateId);
    }

    [Fact]
    public void Match_DecomposedMessage_MatchesComposedTemplate()
    {
        var index = TemplateIndex(new ScamTemplateEntry("t1", "tài khoản của bạn bị khóa"));
        var decomposed = "tài khoản của bạn bị khóa".Normalize(NormalizationForm.FormD);

        var match = index.Match(decomposed, decomposed);

        Assert.Equal("t1", match.Template?.TemplateId);
    }

    [Fact]
    public void Match_UnaccentedTemplate_DoesNotMatchAccentedMessage()
    {
        var index = TemplateIndex(new ScamTemplateEntry("t1", "tai khoan cua ban bi khoa"));

        var match = MatchText(index, "tài khoản của bạn bị khóa");

        Assert.Null(match.Template);
    }

    [Fact]
    public void Match_UnaccentedTemplateAndMessage_ReturnsTemplate()
    {
        var index = TemplateIndex(new ScamTemplateEntry("t1", "tai khoan cua ban bi khoa"));

        var match = MatchText(index, "Tai khoan cua ban bi khoa");

        Assert.Equal("t1", match.Template?.TemplateId);
    }

    [Fact]
    public void Match_ShortTemplateEqualToMessage_ReturnsTemplate()
    {
        var index = TemplateIndex(new ScamTemplateEntry("t1", "khóa tài"));

        var match = MatchText(index, "khóa tài");

        Assert.Equal("t1", match.Template?.TemplateId);
    }

    [Fact]
    public void Match_EmptyTemplateText_NeverMatches()
    {
        var index = TemplateIndex(new ScamTemplateEntry("t1", ""));

        var match = MatchText(index, "a b c");

        Assert.Null(match.Template);
    }

    [Fact]
    public void Match_EmptyMessage_ReturnsNone()
    {
        var index = new ThreatIntelligenceIndex([BlockedDomain], [new ScamTemplateEntry("t1", "a b c")]);

        var match = MatchText(index, "");

        Assert.False(match.IsStrongSignal);
    }

    [Fact]
    public void Match_SingleCharacterMessage_ReturnsNone()
    {
        var index = new ThreatIntelligenceIndex([BlockedDomain], [new ScamTemplateEntry("t1", "a b c")]);

        var match = MatchText(index, "a");

        Assert.False(match.IsStrongSignal);
    }

    [Fact]
    public void Match_MaxLengthMessageWithBlockedUrl_ReturnsDomain()
    {
        var url = "http://bad.example/x";
        var text = url + " " + new string('a', 2000 - url.Length - 1);

        var match = MatchText(DomainIndex(), text);

        Assert.Equal(2000, text.Length);
        Assert.True(match.HasBlocklistedDomain);
    }

    [Fact]
    public void Match_DomainAndTemplate_ReturnsBoth()
    {
        var index = new ThreatIntelligenceIndex([BlockedDomain], [new ScamTemplateEntry("t1", "nhấn <URL> để nhận quà")]);

        var match = MatchText(index, "Nhấn http://bad.example/x để nhận quà");

        Assert.True(match.HasBlocklistedDomain);
        Assert.Equal("t1", match.Template?.TemplateId);
    }

    [Fact]
    public void Empty_AnyText_MatchesNothing()
    {
        var match = MatchText(ThreatIntelligenceIndex.Empty, "Nhấn http://bad.example/x để nhận quà");

        Assert.False(match.IsStrongSignal);
    }

    [Fact]
    public void Counts_EmptyIndex_AreZero()
    {
        Assert.Equal(0, ThreatIntelligenceIndex.Empty.BlockedDomainCount);
        Assert.Equal(0, ThreatIntelligenceIndex.Empty.TemplateCount);
    }

    [Fact]
    public void Counts_DuplicateDomainsAfterNormalization_CountsDistinctDomainsAndAllTemplates()
    {
        var index = new ThreatIntelligenceIndex(
            ["bad.example", "BAD.example ", "other.example"],
            [new ScamTemplateEntry("t1", "a b c"), new ScamTemplateEntry("t2", "d e f")]);

        Assert.Equal(2, index.BlockedDomainCount);
        Assert.Equal(2, index.TemplateCount);
    }

    [Fact]
    public void IsBlocked_TrailingDotHost_StillMatches()
    {
        var blocked = DomainIndex().IsBlocked("x.bad.example.");

        Assert.True(blocked);
    }
}
