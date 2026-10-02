using ScamDetector.Infrastructure.Tests.Support;
using ScamDetector.Infrastructure.Tokenization;

namespace ScamDetector.Infrastructure.Tests.Tokenization;

public sealed class PhoBertTokenizerTests
{
    private const int DefaultMaxSequenceLength = 256;
    private const long XinId = 4;
    private const long ChaoId = 5;
    private const long AContinuationId = 9;
    private const long BcContinuationId = 8;
    private const long DId = 10;

    private readonly PhoBertTokenizer _tokenizer = FixtureTokenizer.Create();

    [Fact]
    public void Encode_KnownWords_WrapsIdsWithSequenceMarkers()
    {
        var ids = _tokenizer.Encode("xin chào", DefaultMaxSequenceLength);

        Assert.Equal([SpecialTokens.BeginOfSequenceId, XinId, ChaoId, SpecialTokens.EndOfSequenceId], ids);
    }

    [Fact]
    public void Encode_WordSplitIntoSubwords_ReturnsSubwordIds()
    {
        var ids = _tokenizer.Encode("abcd", DefaultMaxSequenceLength);

        Assert.Equal([SpecialTokens.BeginOfSequenceId, AContinuationId, BcContinuationId, DId, SpecialTokens.EndOfSequenceId], ids);
    }

    [Fact]
    public void Encode_UnknownWord_ReturnsUnknownIds()
    {
        var ids = _tokenizer.Encode("zq", DefaultMaxSequenceLength);

        Assert.Equal([SpecialTokens.BeginOfSequenceId, SpecialTokens.UnknownId, SpecialTokens.UnknownId, SpecialTokens.EndOfSequenceId], ids);
    }

    [Fact]
    public void Encode_RepeatedWhitespace_IgnoresEmptyWords()
    {
        var ids = _tokenizer.Encode("  xin \n\t chào ", DefaultMaxSequenceLength);

        Assert.Equal([SpecialTokens.BeginOfSequenceId, XinId, ChaoId, SpecialTokens.EndOfSequenceId], ids);
    }

    [Fact]
    public void Encode_TextLongerThanLimit_TruncatesContentAndKeepsEndMarker()
    {
        const int maxSequenceLength = 3;

        var ids = _tokenizer.Encode("xin chào xin", maxSequenceLength);

        Assert.Equal([SpecialTokens.BeginOfSequenceId, XinId, SpecialTokens.EndOfSequenceId], ids);
    }

    [Fact]
    public void Encode_EmptyText_ReturnsOnlySequenceMarkers()
    {
        var ids = _tokenizer.Encode(string.Empty, DefaultMaxSequenceLength);

        Assert.Equal([SpecialTokens.BeginOfSequenceId, SpecialTokens.EndOfSequenceId], ids);
    }
}
