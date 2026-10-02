using System.Text.RegularExpressions;

namespace ScamDetector.Core.Urls;

public static partial class UrlHost
{
    private const string HttpsPrefix = "https://";

    public static string? Parse(string url)
    {
        var absoluteUrl = SchemePrefix().IsMatch(url) ? url : HttpsPrefix + url;
        return Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri)
            ? uri.IdnHost.ToLowerInvariant()
            : null;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9+.\-]*://", RegexOptions.IgnoreCase)]
    private static partial Regex SchemePrefix();
}
