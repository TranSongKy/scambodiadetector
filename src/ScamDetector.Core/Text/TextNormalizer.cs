using System.Text;
using System.Text.RegularExpressions;

namespace ScamDetector.Core.Text;

public static partial class TextNormalizer
{
    public static string Normalize(string text)
    {
        var composedText = text.Normalize(NormalizationForm.FormC);
        return RepeatedWhitespace().Replace(composedText, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex RepeatedWhitespace();
}
