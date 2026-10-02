using ScamDetector.Core.Classification;

namespace ScamDetector.Bot.Messaging;

public static class ReportCallbackData
{
    private const string Prefix = "report:";

    public static string For(MessageLabel label) => Prefix + MessageLabelNames.From(label);

    public static bool TryParse(string? data, out MessageLabel label)
    {
        label = default;
        return data is not null
            && data.StartsWith(Prefix, StringComparison.Ordinal)
            && MessageLabelNames.TryParse(data[Prefix.Length..], out label);
    }
}
