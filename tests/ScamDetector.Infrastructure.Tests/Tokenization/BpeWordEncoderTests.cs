using ScamDetector.Infrastructure.Tokenization;

namespace ScamDetector.Infrastructure.Tests.Tokenization;

public sealed class BpeWordEncoderTests
{
    private static readonly BpeWordEncoder Encoder = new(BpeMergeRanks.FromLines(
    [
        "c h 100",
        "ch à 90",
        "chà o</w> 80",
        "x i 70",
        "b c 40",
        "a b 30",
    ]));

    [Fact]
    public void Encode_FullyMergeableWord_ReturnsSingleSubword()
    {
        var subwords = Encoder.Encode("chào");

        Assert.Equal(["chào"], subwords);
    }

    [Fact]
    public void Encode_PartiallyMergeableWord_MarksNonFinalSubwordsWithContinuation()
    {
        var subwords = Encoder.Encode("xin");

        Assert.Equal(["xi@@", "n"], subwords);
    }

    [Fact]
    public void Encode_CompetingMerges_AppliesLowestRankFirst()
    {
        var subwords = Encoder.Encode("abcd");

        Assert.Equal(["a@@", "bc@@", "d"], subwords);
    }

    [Fact]
    public void Encode_SingleCharacter_ReturnsCharacter()
    {
        var subwords = Encoder.Encode("z");

        Assert.Equal(["z"], subwords);
    }

    [Fact]
    public void Encode_NoMergesApply_SplitsIntoCharacters()
    {
        var subwords = Encoder.Encode("zq");

        Assert.Equal(["z@@", "q"], subwords);
    }

    [Fact]
    public void Encode_SameWordTwice_ReturnsSameResultFromCache()
    {
        var encoder = new BpeWordEncoder(BpeMergeRanks.FromLines(["x i 70"]));

        var first = encoder.Encode("xin");
        var second = encoder.Encode("xin");

        Assert.Same(first, second);
        Assert.Equal(1, encoder.CachedWordCount);
    }

    [Fact]
    public void Encode_ConcurrentCalls_ReturnConsistentResults()
    {
        var encoder = new BpeWordEncoder(BpeMergeRanks.FromLines(["c h 100", "ch à 90", "chà o</w> 80"]));

        var results = Enumerable.Range(0, 200).AsParallel().Select(_ => string.Join('|', encoder.Encode("chào"))).Distinct().ToList();

        Assert.Equal(["chào"], results);
    }
}
