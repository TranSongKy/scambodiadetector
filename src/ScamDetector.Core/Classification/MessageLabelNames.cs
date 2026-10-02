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

    public static MessageLabel Parse(string name) =>
        TryParse(name, out var label) ? label : throw new ArgumentException($"Unknown message label '{name}'.", nameof(name));

    public static bool TryParse(string? name, out MessageLabel label)
    {
        var trimmed = name?.Trim();
        MessageLabel? parsed = Enum.GetValues<MessageLabel>()
            .Select(candidate => (MessageLabel?)candidate)
            .FirstOrDefault(candidate => string.Equals(From(candidate!.Value), trimmed, StringComparison.OrdinalIgnoreCase));
        label = parsed.GetValueOrDefault();
        return parsed.HasValue;
    }
}
