using ScamDetector.Bot.Messaging;
using ScamDetector.Core.Classification;

namespace ScamDetector.Bot.Tests.Messaging;

public sealed class ReportCallbackDataTests
{
    [Theory]
    [InlineData(MessageLabel.Scam)]
    [InlineData(MessageLabel.Spam)]
    [InlineData(MessageLabel.Normal)]
    public void ForThenTryParse_EachLabel_RoundTrips(MessageLabel label)
    {
        var parsed = ReportCallbackData.TryParse(ReportCallbackData.For(label), out var roundTripped);

        Assert.True(parsed);
        Assert.Equal(label, roundTripped);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("report:")]
    [InlineData("report:phishing")]
    [InlineData("other:scam")]
    public void TryParse_InvalidData_ReturnsFalse(string? data)
    {
        Assert.False(ReportCallbackData.TryParse(data, out _));
    }

    [Fact]
    public void For_AnyLabel_FitsTelegramCallbackDataLimit()
    {
        const int telegramCallbackDataLimit = 64;

        Assert.All(Enum.GetValues<MessageLabel>(), label => Assert.True(ReportCallbackData.For(label).Length <= telegramCallbackDataLimit));
    }
}
