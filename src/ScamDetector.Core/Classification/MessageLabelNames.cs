namespace ScamDetector.Core.Classification;

public static class MessageLabelNames
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

    public static MessageLabel Parse(string name) => name.Trim().ToUpperInvariant() switch
    {
        "NORMAL" => MessageLabel.Normal,
        "SPAM" => MessageLabel.Spam,
        "SCAM" => MessageLabel.Scam,
        _ => throw new ArgumentException($"Unknown message label '{name}'.", nameof(name)),
    };
}
