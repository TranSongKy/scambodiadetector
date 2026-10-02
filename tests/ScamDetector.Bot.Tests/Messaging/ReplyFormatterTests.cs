using ScamDetector.Bot.Messaging;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Common;
using ScamDetector.Core.Urls;

namespace ScamDetector.Bot.Tests.Messaging;

public sealed class ReplyFormatterTests
{
    [Fact]
    public void FormatClassification_ScamWithReasons_ContainsVerdictConfidenceReasonsAndAdvice()
    {
        var classification = new ClassificationResult(
            MessageLabel.Scam,
            0.93,
            [ClassificationReasons.ModelPredictedScam, UrlReasons.Shortener]);

        var reply = ReplyFormatter.FormatClassification(classification);

        Assert.StartsWith(BotReplies.ScamVerdict, reply, StringComparison.Ordinal);
        Assert.Contains($"{BotReplies.ConfidenceLabel}: 93", reply, StringComparison.Ordinal);
        Assert.Contains(BotReplies.ReasonsLabel, reply, StringComparison.Ordinal);
        Assert.Contains($"• {ReasonDescriptions.Describe(ClassificationReasons.ModelPredictedScam)}", reply, StringComparison.Ordinal);
        Assert.Contains($"• {ReasonDescriptions.Describe(UrlReasons.Shortener)}", reply, StringComparison.Ordinal);
        Assert.EndsWith(BotReplies.ScamAdvice, reply, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MessageLabel.Spam, BotReplies.SpamVerdict)]
    [InlineData(MessageLabel.Normal, BotReplies.NormalVerdict)]
    public void FormatClassification_NonScamLabel_ShowsVerdictWithoutAdvice(MessageLabel label, string expectedVerdict)
    {
        var classification = new ClassificationResult(label, 0.8, []);

        var reply = ReplyFormatter.FormatClassification(classification);

        Assert.StartsWith(expectedVerdict, reply, StringComparison.Ordinal);
        Assert.DoesNotContain(BotReplies.ScamAdvice, reply, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatClassification_NoReasons_OmitsReasonsSection()
    {
        var classification = new ClassificationResult(MessageLabel.Normal, 0.99, []);

        var reply = ReplyFormatter.FormatClassification(classification);

        Assert.DoesNotContain(BotReplies.ReasonsLabel, reply, StringComparison.Ordinal);
        Assert.DoesNotContain("•", reply, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatClassification_DuplicateReasonCodes_ShowsReasonOnce()
    {
        var classification = new ClassificationResult(
            MessageLabel.Scam,
            0.9,
            [UrlReasons.Shortener, UrlReasons.Shortener]);

        var reply = ReplyFormatter.FormatClassification(classification);

        var description = ReasonDescriptions.Describe(UrlReasons.Shortener);
        Assert.Equal(1, reply.Split(description).Length - 1);
    }

    [Fact]
    public void FormatClassification_UnknownReasonCode_ShowsCode()
    {
        var classification = new ClassificationResult(MessageLabel.Spam, 0.7, ["mystery_reason"]);

        var reply = ReplyFormatter.FormatClassification(classification);

        Assert.Contains("• mystery_reason", reply, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatClassification_AnyResult_HasNoTrailingWhitespace()
    {
        var classification = new ClassificationResult(MessageLabel.Normal, 0.5, [UrlReasons.Punycode]);

        var reply = ReplyFormatter.FormatClassification(classification);

        Assert.Equal(reply.TrimEnd(), reply);
    }

    [Fact]
    public void FormatError_EmptyText_ReturnsEmptyTextReply()
    {
        var reply = ReplyFormatter.FormatError(ClassificationErrors.EmptyText);

        Assert.Equal(BotReplies.EmptyText, reply);
    }

    [Fact]
    public void FormatError_TextTooLong_ReturnsTextTooLongReply()
    {
        var reply = ReplyFormatter.FormatError(ClassificationErrors.TextTooLong);

        Assert.Equal(BotReplies.TextTooLong, reply);
    }

    [Fact]
    public void FormatError_OtherDomainError_ReturnsInvalidTextReply()
    {
        var reply = ReplyFormatter.FormatError(new DomainError("other.error", "Other."));

        Assert.Equal(BotReplies.InvalidText, reply);
    }
}
