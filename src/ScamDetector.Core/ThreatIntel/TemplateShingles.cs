using System.Text;
using System.Text.RegularExpressions;

namespace ScamDetector.Core.ThreatIntel;

public static partial class TemplateShingles
{
    public const int ShingleSize = 3;
    private const char PlaceholderStart = '<';
    private const string TokenSeparator = " ";

    public static IReadOnlyList<string> Tokenize(string text) =>
        TokenPattern().Matches(text.Normalize(NormalizationForm.FormC))
            .Select(match => match.Value[0] == PlaceholderStart ? match.Value : match.Value.ToLowerInvariant())
            .ToList();

    public static IReadOnlySet<string> Build(string text)
    {
        var tokens = Tokenize(text);
        if (tokens.Count < ShingleSize)
            return tokens.Count == 0 ? new HashSet<string>() : new HashSet<string> { string.Join(TokenSeparator, tokens) };

        return Enumerable.Range(0, tokens.Count - ShingleSize + 1)
            .Select(index => string.Join(TokenSeparator, tokens.Skip(index).Take(ShingleSize)))
            .ToHashSet(StringComparer.Ordinal);
    }

    public static double Coverage(IReadOnlySet<string> templateShingles, IReadOnlySet<string> messageShingles) =>
        templateShingles.Count == 0
            ? 0
            : (double)templateShingles.Count(messageShingles.Contains) / templateShingles.Count;

    [GeneratedRegex(@"<[A-Z]+>|[^\W_]+")]
    private static partial Regex TokenPattern();
}
