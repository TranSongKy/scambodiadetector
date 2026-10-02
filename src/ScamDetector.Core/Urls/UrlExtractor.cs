using System.Text.RegularExpressions;

namespace ScamDetector.Core.Urls;

public static partial class UrlExtractor
{
    private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '!', '?', ')', ']', '}', '"', '\''];

    public static IReadOnlyList<string> Extract(string text) =>
        UrlPattern().Matches(text)
            .Select(match => match.Value.TrimEnd(TrailingPunctuation))
            .ToList();

    public static string Replace(string text, string replacement) =>
        UrlPattern().Replace(text, match => replacement + match.Value[match.Value.TrimEnd(TrailingPunctuation).Length..]);

    [GeneratedRegex(
        @"(?:https?://|www\.)[^\s<>""]+|\b(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+(?-i:(?:com|vn|net|org|info|xyz|top|me|ly|io|cc|online|site|shop|link|club|icu|vip|live|buzz|tk|ml|ga|cf|gq|gl|gd|id|at|gy)|(?:COM|VN|NET|ORG|INFO|XYZ|TOP|ME|LY|IO|CC|ONLINE|SITE|SHOP|LINK|CLUB|ICU|VIP|LIVE|BUZZ|TK|ML|GA|CF|GQ|GL|GD|ID|AT|GY))\b(?:/[^\s<>""]*)?",
        RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
}
