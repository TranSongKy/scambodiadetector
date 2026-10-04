using ScamDetector.Bot.Messaging;
using ScamDetector.Bot.Telegram;
using ScamDetector.Bot.Tests.Fakes;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Common;
using ScamDetector.Infrastructure.Onnx;

namespace ScamDetector.Bot.Tests.Messaging;

public sealed class BotReplyBuilderTests
{
    private static readonly ClassificationResult NormalResult = new(MessageLabel.Normal, 0.9, []);

    private static TelegramMessage MessageWith(string? text, string? caption = null) =>
        new(7, new TelegramChat(42), text, caption);

    private static FakeMessageClassifier SuccessfulClassifier() =>
        new(Result.Success(NormalResult));

    [Theory]
    [InlineData("/start")]
    [InlineData("/help")]
    [InlineData("/start@SomeBot")]
    [InlineData("/start\nxin chao")]
    [InlineData("/HELP extra")]
    [InlineData("  /Start  ")]
    public async Task BuildReplyAsync_CommandText_ReturnsWelcomeWithoutClassifying(string text)
    {
        var classifier = SuccessfulClassifier();
        var builder = new BotReplyBuilder(classifier);

        var reply = await builder.BuildReplyAsync(MessageWith(text), CancellationToken.None);

        Assert.Equal(BotReplies.Welcome, reply?.Text);
        Assert.Empty(classifier.ReceivedTexts);
    }

    [Fact]
    public async Task BuildReplyAsync_NoTextAndNoCaptionInGroup_ReturnsNull()
    {
        var classifier = SuccessfulClassifier();
        var builder = new BotReplyBuilder(classifier);

        var reply = await builder.BuildReplyAsync(MessageWith(null), CancellationToken.None);

        Assert.Null(reply);
        Assert.Empty(classifier.ReceivedTexts);
    }

    [Fact]
    public async Task BuildReplyAsync_NoTextAndNoCaptionInPrivateChat_ReturnsTextOnlyHint()
    {
        var classifier = SuccessfulClassifier();
        var builder = new BotReplyBuilder(classifier);
        var photoOnly = new TelegramMessage(7, new TelegramChat(42, "private"), Text: null, Caption: null);

        var reply = await builder.BuildReplyAsync(photoOnly, CancellationToken.None);

        Assert.Equal(BotReplies.TextOnly, reply?.Text);
        Assert.False(reply?.OfferReport);
        Assert.Empty(classifier.ReceivedTexts);
    }

    [Fact]
    public async Task BuildReplyAsync_NullTextWithCaption_ClassifiesCaption()
    {
        var classifier = SuccessfulClassifier();
        var builder = new BotReplyBuilder(classifier);

        var reply = await builder.BuildReplyAsync(MessageWith(null, "chu thich"), CancellationToken.None);

        Assert.Equal(["chu thich"], classifier.ReceivedTexts);
        Assert.StartsWith(BotReplies.NormalVerdict, reply?.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildReplyAsync_TextAndCaption_ClassifiesText()
    {
        var classifier = SuccessfulClassifier();
        var builder = new BotReplyBuilder(classifier);

        await builder.BuildReplyAsync(MessageWith("noi dung", "chu thich"), CancellationToken.None);

        Assert.Equal(["noi dung"], classifier.ReceivedTexts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BuildReplyAsync_BlankTextRejectedByClassifier_ReturnsEmptyTextReply(string text)
    {
        var classifier = new FakeMessageClassifier(Result.Failure<ClassificationResult>(ClassificationErrors.EmptyText));
        var builder = new BotReplyBuilder(classifier);

        var reply = await builder.BuildReplyAsync(MessageWith(text), CancellationToken.None);

        Assert.Equal(BotReplies.EmptyText, reply?.Text);
    }

    [Fact]
    public async Task BuildReplyAsync_TextTooLong_ReturnsTextTooLongReply()
    {
        var classifier = new FakeMessageClassifier(Result.Failure<ClassificationResult>(ClassificationErrors.TextTooLong));
        var builder = new BotReplyBuilder(classifier);

        var reply = await builder.BuildReplyAsync(MessageWith(new string('a', 2001)), CancellationToken.None);

        Assert.Equal(BotReplies.TextTooLong, reply?.Text);
    }

    [Fact]
    public async Task BuildReplyAsync_ScamResult_ReturnsFormattedScamReply()
    {
        var scam = new ClassificationResult(MessageLabel.Scam, 0.95, [ClassificationReasons.ModelPredictedScam]);
        var builder = new BotReplyBuilder(new FakeMessageClassifier(Result.Success(scam)));

        var reply = await builder.BuildReplyAsync(MessageWith("tin nhan"), CancellationToken.None);

        Assert.Equal(ReplyFormatter.FormatClassification(scam), reply?.Text);
    }

    [Fact]
    public async Task BuildReplyAsync_ClassifierThrowsModelUnavailable_ReturnsModelUnavailableReply()
    {
        var classifier = new FakeMessageClassifier(new ScamModelUnavailableException("down"));
        var builder = new BotReplyBuilder(classifier);

        var reply = await builder.BuildReplyAsync(MessageWith("tin nhan"), CancellationToken.None);

        Assert.Equal(BotReplies.ModelUnavailable, reply?.Text);
    }

    [Fact]
    public async Task BuildReplyAsync_ClassifierThrowsOtherException_Propagates()
    {
        var classifier = new FakeMessageClassifier(new InvalidOperationException("boom"));
        var builder = new BotReplyBuilder(classifier);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => builder.BuildReplyAsync(MessageWith("tin nhan"), CancellationToken.None));
    }
}
