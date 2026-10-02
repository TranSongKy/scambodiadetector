using ScamDetector.Core.Classification;

namespace ScamDetector.Api.Classifications;

public static class LabelNames
{
    public const string Normal = "normal";
    public const string Spam = "spam";
    public const string Scam = "scam";

    public static string From(MessageLabel label) => label switch
    {
        MessageLabel.Normal => Normal,
        MessageLabel.Spam => Spam,
        MessageLabel.Scam => Scam,
        _ => throw new ArgumentOutOfRangeException(nameof(label), label, null),
    };
}
