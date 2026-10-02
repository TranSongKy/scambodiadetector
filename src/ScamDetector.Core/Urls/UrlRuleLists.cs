namespace ScamDetector.Core.Urls;

public static class UrlRuleLists
{
    public const string PunycodePrefix = "xn--";

    public static readonly IReadOnlySet<string> ShortenerHosts = new HashSet<string>(StringComparer.Ordinal)
    {
        "bit.ly", "tinyurl.com", "t.ly", "cutt.ly", "rb.gy", "is.gd", "s.id",
        "shorturl.at", "goo.gl", "ow.ly", "tiny.cc", "t.co", "rebrand.ly",
    };

    public static readonly IReadOnlySet<string> SuspiciousTopLevelDomains = new HashSet<string>(StringComparer.Ordinal)
    {
        "xyz", "top", "club", "icu", "online", "site", "vip", "shop", "live",
        "buzz", "cc", "tk", "ml", "ga", "cf", "gq",
    };
}
