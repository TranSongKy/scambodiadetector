using ScamDetector.Core.Text;

namespace ScamDetector.Core.Tests.Text;

public sealed class EditDistanceTests
{
    [Theory]
    [InlineData("abc", "abc", 0)]
    [InlineData("", "", 0)]
    [InlineData("abc", "abd", 1)]
    [InlineData("abc", "abcd", 1)]
    [InlineData("abcd", "abc", 1)]
    [InlineData("abcd", "abdc", 1)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "", 3)]
    [InlineData("a", "b", 1)]
    [InlineData("khóa", "khoa", 1)]
    [InlineData("kitten", "sitting", 3)]
    public void OptimalStringAlignment_Pair_ReturnsExpectedDistance(string first, string second, int expected)
    {
        var distance = EditDistance.OptimalStringAlignment(first, second);

        Assert.Equal(expected, distance);
    }

    [Fact]
    public void OptimalStringAlignment_AdjacentTranspositionAtStart_ReturnsOne()
    {
        var distance = EditDistance.OptimalStringAlignment("ba", "ab");

        Assert.Equal(1, distance);
    }

    [Fact]
    public void OptimalStringAlignment_LongStringsWithOneSubstitution_ReturnsOne()
    {
        var first = new string('a', 2000);
        var second = new string('a', 1999) + "b";

        var distance = EditDistance.OptimalStringAlignment(first, second);

        Assert.Equal(1, distance);
    }

    [Theory]
    [InlineData("abc", "abc", 1, true)]
    [InlineData("abc", "abd", 1, true)]
    [InlineData("abc", "abdd", 1, false)]
    [InlineData("abc", "abdd", 2, true)]
    [InlineData("abcd", "abdc", 1, true)]
    [InlineData("abc", "xyz", 2, false)]
    [InlineData("abc", "abcdef", 2, false)]
    [InlineData("abc", "abc", 0, true)]
    [InlineData("abc", "abd", 0, false)]
    [InlineData("", "a", 1, true)]
    [InlineData("", "ab", 1, false)]
    public void IsWithin_Pair_ReturnsExpected(string first, string second, int maximumDistance, bool expected)
    {
        var isWithin = EditDistance.IsWithin(first, second, maximumDistance);

        Assert.Equal(expected, isWithin);
    }
}
