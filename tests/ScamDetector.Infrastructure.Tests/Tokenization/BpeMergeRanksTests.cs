using ScamDetector.Infrastructure.Tokenization;

namespace ScamDetector.Infrastructure.Tests.Tokenization;

public sealed class BpeMergeRanksTests
{
    [Fact]
    public void RankOf_PairsInFileOrder_ReturnsLineIndex()
    {
        var ranks = BpeMergeRanks.FromLines(["a b 9", "c d 8"]);

        Assert.Equal(0, ranks.RankOf("a", "b"));
        Assert.Equal(1, ranks.RankOf("c", "d"));
    }

    [Fact]
    public void RankOf_UnknownPair_ReturnsNull()
    {
        var ranks = BpeMergeRanks.FromLines(["a b 9"]);

        Assert.Null(ranks.RankOf("b", "a"));
    }

    [Fact]
    public void FromLines_BlankAndMalformedLines_AreSkipped()
    {
        var ranks = BpeMergeRanks.FromLines(["", "single", "a b 9"]);

        Assert.Equal(0, ranks.RankOf("a", "b"));
    }
}
