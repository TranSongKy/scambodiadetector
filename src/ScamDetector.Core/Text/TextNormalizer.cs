using System.Text;
using System.Text.RegularExpressions;

namespace ScamDetector.Core.Text;

public static partial class TextNormalizer
{
    public static string Normalize(string text)
    {
        var composedText = text.Normalize(NormalizationForm.FormC);
        var visibleText = InvisibleCharacters().Replace(composedText, string.Empty);
        return RepeatedWhitespace().Replace(visibleText, " ").Trim();
    }

    [GeneratedRegex("[\u00AD\u200B-\u200D\u2060\uFEFF]")]
    private static partial Regex InvisibleCharacters();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RepeatedWhitespace();
}
