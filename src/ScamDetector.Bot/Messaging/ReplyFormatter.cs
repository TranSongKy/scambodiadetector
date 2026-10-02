using System.Globalization;
using System.Text;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Common;

namespace ScamDetector.Bot.Messaging;

public static class ReplyFormatter
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static string FormatClassification(ClassificationResult classification)
    {
        var reply = new StringBuilder()
            .AppendLine(Verdict(classification.Label))
            .AppendLine(CultureInfo.InvariantCulture, $"{BotReplies.ConfidenceLabel}: {classification.Confidence.ToString("P0", VietnameseCulture)}");

        var reasons = classification.Reasons.Distinct(StringComparer.Ordinal).Select(ReasonDescriptions.Describe).ToList();
        if (reasons.Count > 0)
        {
            reply.AppendLine(CultureInfo.InvariantCulture, $"{BotReplies.ReasonsLabel}:");
            reasons.ForEach(reason => reply.AppendLine(CultureInfo.InvariantCulture, $"• {reason}"));
        }
        if (classification.Label == MessageLabel.Scam)
            reply.AppendLine().Append(BotReplies.ScamAdvice);

        return reply.ToString().TrimEnd();
    }

    public static string FormatError(DomainError error) =>
        error == ClassificationErrors.EmptyText ? BotReplies.EmptyText
        : error == ClassificationErrors.TextTooLong ? BotReplies.TextTooLong
        : BotReplies.InvalidText;

    private static string Verdict(MessageLabel label) => label switch
    {
        MessageLabel.Scam => BotReplies.ScamVerdict,
        MessageLabel.Spam => BotReplies.SpamVerdict,
        _ => BotReplies.NormalVerdict,
    };
}
