namespace ScamDetector.Core.Reports;

public static class ReportChannelNames
{
    public static bool TryParse(string? name, out ReportChannel channel)
    {
        ReportChannel? parsed = name?.Trim().ToUpperInvariant() switch
        {
            "API" => ReportChannel.Api,
            "TELEGRAM" => ReportChannel.Telegram,
            "EXTENSION" => ReportChannel.Extension,
            _ => null,
        };
        channel = parsed.GetValueOrDefault();
        return parsed.HasValue;
    }
}
