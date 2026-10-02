using ScamDetector.Core.Classification;

namespace ScamDetector.Core.Tests.Classification;

public sealed class MessageLabelNamesTests
{
    [Theory]
    [InlineData(MessageLabel.Normal, MessageLabelNames.Normal)]
    [InlineData(MessageLabel.Spam, MessageLabelNames.Spam)]
    [InlineData(MessageLabel.Scam, MessageLabelNames.Scam)]
    public void From_EachLabel_ReturnsLowercaseName(MessageLabel label, string expected)
    {
        var name = MessageLabelNames.From(label);

        Assert.Equal(expected, name);
    }

    [Fact]
    public void From_UndefinedLabel_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MessageLabelNames.From((MessageLabel)99));
    }

    [Theory]
    [InlineData("scam", MessageLabel.Scam)]
    [InlineData("SPAM", MessageLabel.Spam)]
    [InlineData(" Normal ", MessageLabel.Normal)]
    public void Parse_KnownNameAnyCase_ReturnsLabel(string name, MessageLabel expected)
    {
        var label = MessageLabelNames.Parse(name);

        Assert.Equal(expected, label);
    }

    [Theory]
    [InlineData("phishing")]
    [InlineData("")]
    public void Parse_UnknownName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() => MessageLabelNames.Parse(name));
    }

    [Fact]
    public void FromThenParse_EveryLabel_RoundTrips()
    {
        var labels = Enum.GetValues<MessageLabel>();

        var roundTripped = labels.Select(label => MessageLabelNames.Parse(MessageLabelNames.From(label)));

        Assert.Equal(labels, roundTripped);
    }
}
