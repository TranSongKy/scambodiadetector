namespace ScamDetector.Core.Reports;

public static class ReportChannelNames
{
    public const string Api = "api";
    public const string Telegram = "telegram";
    public const string Extension = "extension";

    private static readonly IReadOnlyList<(string Name, ReportChannel Channel)> Channels =
    [
        (Api, ReportChannel.Api),
        (Telegram, ReportChannel.Telegram),
        (Extension, ReportChannel.Extension),
    ];

    public static bool TryParse(string? name, out ReportChannel channel)
    {
        var trimmed = name?.Trim();
        var match = Channels.FirstOrDefault(entry => string.Equals(entry.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        channel = match.Channel;
        return match.Name is not null;
    }
}
