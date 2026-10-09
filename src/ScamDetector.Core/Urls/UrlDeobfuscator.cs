using System.Text.RegularExpressions;

namespace ScamDetector.Core.Urls;

public static partial class UrlDeobfuscator
{
    private const string Dot = ".";
    private const string RestoredScheme = "http$1://";

    public static string Restore(string text)
    {
        var restoredText = BracketedDot().Replace(text, Dot);
        return DefangedScheme().Replace(restoredText, RestoredScheme);
    }

    [GeneratedRegex(@"\s?[\[\(\{]\s?(?:\.|dot|ch[ấa]m)\s?[\]\)\}]\s?", RegexOptions.IgnoreCase)]
    private static partial Regex BracketedDot();

    [GeneratedRegex(@"\bh(?:xx|\*\*)p(s?)(?:\[:\]|:)//", RegexOptions.IgnoreCase)]
    private static partial Regex DefangedScheme();
}
