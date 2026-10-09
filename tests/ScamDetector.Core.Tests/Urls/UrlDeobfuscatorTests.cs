using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Urls;

public sealed class UrlDeobfuscatorTests
{
    [Theory]
    [InlineData("vtp-vandon[.]online", "vtp-vandon.online")]
    [InlineData("vtp-vandon(.)online", "vtp-vandon.online")]
    [InlineData("vtp-vandon{.}online", "vtp-vandon.online")]
    [InlineData("vtp-vandon[dot]online", "vtp-vandon.online")]
    [InlineData("vtp-vandon(chấm)online", "vtp-vandon.online")]
    [InlineData("vtp-vandon[cham]online", "vtp-vandon.online")]
    [InlineData("vtp-vandon [.] online", "vtp-vandon.online")]
    [InlineData("vtp-vandon [dot] online", "vtp-vandon.online")]
    [InlineData("vtp-vandon[DOT]online", "vtp-vandon.online")]
    [InlineData("vtp-vandon(CHẤM)online", "vtp-vandon.online")]
    [InlineData("vtp-vandon[Cham]online", "vtp-vandon.online")]
    public void Restore_BracketedDot_ReturnsPlainDot(string text, string expected)
    {
        var restored = UrlDeobfuscator.Restore(text);

        Assert.Equal(expected, restored);
    }

    [Fact]
    public void Restore_MultipleBracketedDots_RestoresAll()
    {
        var restored = UrlDeobfuscator.Restore("a[.]b(.)example[dot]com");

        Assert.Equal("a.b.example.com", restored);
    }

    [Theory]
    [InlineData("hxxp://a.example", "http://a.example")]
    [InlineData("hxxps://a.example", "https://a.example")]
    [InlineData("hxxps[:]//a.example", "https://a.example")]
    [InlineData("hxxp[:]//a.example", "http://a.example")]
    [InlineData("h**ps://a.example", "https://a.example")]
    [InlineData("h**p://a.example", "http://a.example")]
    [InlineData("HXXPS://a.example", "httpS://a.example")]
    public void Restore_DefangedScheme_ReturnsHttpScheme(string text, string expected)
    {
        var restored = UrlDeobfuscator.Restore(text);

        Assert.Equal(expected, restored);
    }

    [Fact]
    public void Restore_DefangedSchemeAndBracketedDot_RestoresBoth()
    {
        var restored = UrlDeobfuscator.Restore("hxxps://vtp-vandon[.]online/x");

        Assert.Equal("https://vtp-vandon.online/x", restored);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Gặp nhau (chiều nay) nhé")]
    [InlineData("Gap nhau (chieu nay) nhe")]
    [InlineData("[Thông báo]")]
    [InlineData("[Thong bao]")]
    [InlineData("xem https://a.example/x")]
    [InlineData("Tài khoản của bạn bị khóa")]
    public void Restore_OrdinaryText_ReturnsUnchanged(string text)
    {
        var restored = UrlDeobfuscator.Restore(text);

        Assert.Equal(text, restored);
    }

    [Fact]
    public void Restore_MaximumLengthText_ReturnsSameLength()
    {
        var text = new string('a', 2000);

        var restored = UrlDeobfuscator.Restore(text);

        Assert.Equal(2000, restored.Length);
    }

    [Fact]
    public void Restore_SingleCharacter_ReturnsUnchanged()
    {
        var restored = UrlDeobfuscator.Restore("[");

        Assert.Equal("[", restored);
    }

    [Fact]
    public void Restore_DecomposedChamInput_StillMatchesAfterNfcNormalization()
    {
        var text = "vtp-vandon(chấm)online".Normalize(System.Text.NormalizationForm.FormC);

        var restored = UrlDeobfuscator.Restore(text);

        Assert.Equal("vtp-vandon.online", restored);
    }
}
