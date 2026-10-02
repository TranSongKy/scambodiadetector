using System.Text;
using ScamDetector.Core.Text;

namespace ScamDetector.Core.Tests.Text;

public sealed class TextNormalizerTests
{
    private const string ComposedAcuteE = "\u00e9";
    private const string DecomposedAcuteE = "e\u0301";
    private const string AccentedSentence = "Chuyển khoản ngay hôm nay";
    private const string UnaccentedSentence = "Chuyen khoan ngay hom nay";

    [Fact]
    public void Normalize_DecomposedVietnamese_ReturnsComposedForm()
    {
        var result = TextNormalizer.Normalize(DecomposedAcuteE);

        Assert.Equal(ComposedAcuteE, result);
    }

    [Fact]
    public void Normalize_DecomposedSentence_EqualsComposedSentence()
    {
        var decomposedSentence = AccentedSentence.Normalize(NormalizationForm.FormD);

        var result = TextNormalizer.Normalize(decomposedSentence);

        Assert.NotEqual(AccentedSentence, decomposedSentence);
        Assert.Equal(AccentedSentence, result);
        Assert.True(result.IsNormalized(NormalizationForm.FormC));
    }

    [Fact]
    public void Normalize_ComposedVietnamese_ReturnsUnchanged()
    {
        var result = TextNormalizer.Normalize(AccentedSentence);

        Assert.Equal(AccentedSentence, result);
    }

    [Fact]
    public void Normalize_VietnameseWithoutDiacritics_ReturnsUnchanged()
    {
        var result = TextNormalizer.Normalize(UnaccentedSentence);

        Assert.Equal(UnaccentedSentence, result);
    }

    [Fact]
    public void Normalize_RepeatedSpaces_CollapsesToSingleSpace()
    {
        var result = TextNormalizer.Normalize("chuyển     khoản   ngay");

        Assert.Equal("chuyển khoản ngay", result);
    }

    [Fact]
    public void Normalize_TabsAndNewlines_CollapsesToSingleSpace()
    {
        var result = TextNormalizer.Normalize("một\t\thai\r\n\r\nba");

        Assert.Equal("một hai ba", result);
    }

    [Fact]
    public void Normalize_LeadingAndTrailingWhitespace_Trims()
    {
        var result = TextNormalizer.Normalize("  \t xin chào \n ");

        Assert.Equal("xin chào", result);
    }

    [Fact]
    public void Normalize_NonBreakingSpace_TreatedAsWhitespace()
    {
        var result = TextNormalizer.Normalize("xin\u00a0\u00a0chào");

        Assert.Equal("xin chào", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" \t\r\n ")]
    public void Normalize_EmptyOrWhitespace_ReturnsEmpty(string text)
    {
        var result = TextNormalizer.Normalize(text);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Normalize_SingleCharacter_ReturnsSameCharacter()
    {
        var result = TextNormalizer.Normalize("a");

        Assert.Equal("a", result);
    }

    [Fact]
    public void Normalize_AlreadyNormalizedText_IsIdempotent()
    {
        var once = TextNormalizer.Normalize("  e\u0301   xin  chào ");

        var twice = TextNormalizer.Normalize(once);

        Assert.Equal(once, twice);
    }
}
