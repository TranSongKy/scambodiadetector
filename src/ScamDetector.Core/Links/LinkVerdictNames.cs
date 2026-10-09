namespace ScamDetector.Core.Links;

public static class LinkVerdictNames
{
    public const string Safe = "safe";
    public const string Suspicious = "suspicious";
    public const string Dangerous = "dangerous";

    public static string From(LinkVerdict verdict) => verdict switch
    {
        LinkVerdict.Safe => Safe,
        LinkVerdict.Suspicious => Suspicious,
        LinkVerdict.Dangerous => Dangerous,
        _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, null),
    };
}
