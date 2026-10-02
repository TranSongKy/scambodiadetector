using System.Globalization;
using System.Text;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Common;

namespace ScamDetector.Bot.Messaging;

public static class ReplyFormatter
{
    private const string VietnameseCultureName = "vi-VN";
    private const string WholePercentFormat = "P0";
    private const string BulletPrefix = "• ";

    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo(VietnameseCultureName);

    public static string FormatClassification(ClassificationResult classification)
    {
        var reply = new StringBuilder()
            .AppendLine(Verdict(classification.Label))
            .AppendLine(CultureInfo.InvariantCulture, $"{BotReplies.ConfidenceLabel}: {classification.Confidence.ToString(WholePercentFormat, VietnameseCulture)}");

        var reasons = classification.Reasons.Distinct(StringComparer.Ordinal).Select(ReasonDescriptions.Describe).ToList();
        if (reasons.Count > 0)
        {
            reply.AppendLine(CultureInfo.InvariantCulture, $"{BotReplies.ReasonsLabel}:");
            reasons.ForEach(reason => reply.AppendLine(CultureInfo.InvariantCulture, $"{BulletPrefix}{reason}"));
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
