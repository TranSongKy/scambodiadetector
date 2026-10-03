using System.Text;
using ScamDetector.Core.ThreatIntel;

namespace ScamDetector.Core.Tests.ThreatIntel;

public sealed class TemplateShinglesTests
{
    [Fact]
    public void Tokenize_UrlPlaceholder_KeepsUpperCase()
    {
        var tokens = TemplateShingles.Tokenize("Nhấn <URL> ngay");

        Assert.Equal(["nhấn", "<URL>", "ngay"], tokens);
    }

    [Fact]
    public void Tokenize_MixedCaseText_ReturnsLowerCaseTokens()
    {
        var tokens = TemplateShingles.Tokenize("TÀI Khoản BỊ Khóa");

        Assert.Equal(["tài", "khoản", "bị", "khóa"], tokens);
    }

    [Fact]
    public void Tokenize_PunctuationAndUnderscore_SplitsOnThem()
    {
        var tokens = TemplateShingles.Tokenize("a,b_c!  d");

        Assert.Equal(["a", "b", "c", "d"], tokens);
    }

    [Fact]
    public void Tokenize_DecomposedVietnamese_EqualsComposedTokens()
    {
        var composed = "Tài khoản bị khóa";
        var decomposed = composed.Normalize(NormalizationForm.FormD);

        var tokens = TemplateShingles.Tokenize(decomposed);

        Assert.NotEqual(composed, decomposed);
        Assert.Equal(TemplateShingles.Tokenize(composed), tokens);
        Assert.Equal(["tài", "khoản", "bị", "khóa"], tokens);
    }

    [Fact]
    public void Tokenize_UnaccentedVietnamese_ReturnsTokensUnchanged()
    {
        var tokens = TemplateShingles.Tokenize("Tai khoan bi khoa");

        Assert.Equal(["tai", "khoan", "bi", "khoa"], tokens);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ,,,")]
    public void Tokenize_EmptyOrSymbolsOnly_ReturnsEmpty(string text)
    {
        var tokens = TemplateShingles.Tokenize(text);

        Assert.Empty(tokens);
    }

    [Fact]
    public void Build_FourTokens_ReturnsTwoTrigrams()
    {
        var shingles = TemplateShingles.Build("a b c d");

        Assert.Equal(new HashSet<string> { "a b c", "b c d" }, shingles);
    }

    [Fact]
    public void Build_ExactlyThreeTokens_ReturnsSingleTrigram()
    {
        var shingles = TemplateShingles.Build("a b c");

        Assert.Equal(new HashSet<string> { "a b c" }, shingles);
    }

    [Fact]
    public void Build_RepeatedTrigrams_ReturnsDistinctShingles()
    {
        var shingles = TemplateShingles.Build("a b a b a b");

        Assert.Equal(new HashSet<string> { "a b a", "b a b" }, shingles);
    }

    [Theory]
    [InlineData("a", "a")]
    [InlineData("a b", "a b")]
    public void Build_FewerThanThreeTokens_ReturnsSingleShingleOfAllTokens(string text, string expected)
    {
        var shingles = TemplateShingles.Build(text);

        Assert.Equal(new HashSet<string> { expected }, shingles);
    }

    [Fact]
    public void Build_EmptyText_ReturnsEmptySet()
    {
        var shingles = TemplateShingles.Build("");

        Assert.Empty(shingles);
    }

    [Fact]
    public void Build_UrlPlaceholder_KeepsPlaceholderInShingle()
    {
        var shingles = TemplateShingles.Build("Nhấn <URL> ngay");

        Assert.Equal(new HashSet<string> { "nhấn <URL> ngay" }, shingles);
    }

    [Fact]
    public void Coverage_AllTemplateShinglesPresent_ReturnsOne()
    {
        var template = TemplateShingles.Build("a b c d");
        var message = TemplateShingles.Build("x a b c d y");

        var coverage = TemplateShingles.Coverage(template, message);

        Assert.Equal(1.0, coverage);
    }

    [Fact]
    public void Coverage_HalfOfTemplateShinglesPresent_ReturnsHalf()
    {
        var template = TemplateShingles.Build("a b c d");
        var message = TemplateShingles.Build("a b c x");

        var coverage = TemplateShingles.Coverage(template, message);

        Assert.Equal(0.5, coverage);
    }

    [Fact]
    public void Coverage_NoOverlap_ReturnsZero()
    {
        var template = TemplateShingles.Build("a b c d");
        var message = TemplateShingles.Build("w x y z");

        var coverage = TemplateShingles.Coverage(template, message);

        Assert.Equal(0.0, coverage);
    }

    [Fact]
    public void Coverage_EmptyTemplate_ReturnsZero()
    {
        var template = TemplateShingles.Build("");
        var message = TemplateShingles.Build("a b c");

        var coverage = TemplateShingles.Coverage(template, message);

        Assert.Equal(0.0, coverage);
    }

    [Fact]
    public void Coverage_EmptyMessage_ReturnsZero()
    {
        var template = TemplateShingles.Build("a b c");
        var message = TemplateShingles.Build("");

        var coverage = TemplateShingles.Coverage(template, message);

        Assert.Equal(0.0, coverage);
    }

    [Fact]
    public void Coverage_TemplateShorterThanThreeTokens_MatchesWhenMessageHasSameShortShingle()
    {
        var template = TemplateShingles.Build("khóa tài");
        var message = TemplateShingles.Build("khóa tài");

        var coverage = TemplateShingles.Coverage(template, message);

        Assert.Equal(1.0, coverage);
    }

    [Fact]
    public void Coverage_TemplateShorterThanThreeTokens_DoesNotMatchInsideLongerMessage()
    {
        var template = TemplateShingles.Build("khóa tài");
        var message = TemplateShingles.Build("bị khóa tài khoản");

        var coverage = TemplateShingles.Coverage(template, message);

        Assert.Equal(0.0, coverage);
    }
}
