using ScamDetector.Core.Links;

namespace ScamDetector.Core.Tests.Links;

public sealed class LinkVerdictNamesTests
{
    [Theory]
    [InlineData(LinkVerdict.Safe, "safe")]
    [InlineData(LinkVerdict.Suspicious, "suspicious")]
    [InlineData(LinkVerdict.Dangerous, "dangerous")]
    public void From_DefinedVerdict_ReturnsName(LinkVerdict verdict, string expectedName)
    {
        var name = LinkVerdictNames.From(verdict);

        Assert.Equal(expectedName, name);
    }

    [Fact]
    public void From_UndefinedVerdict_ThrowsArgumentOutOfRange()
    {
        var undefinedVerdict = (LinkVerdict)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => LinkVerdictNames.From(undefinedVerdict));
    }
}
