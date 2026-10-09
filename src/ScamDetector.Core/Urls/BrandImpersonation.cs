using ScamDetector.Core.Text;

namespace ScamDetector.Core.Urls;

public static class BrandImpersonation
{
    public const int MinimumSubstringKeywordLength = 5;
    public const int MinimumTypoKeywordLength = 8;
    private const int MaximumTypoDistance = 1;

    private static readonly char[] TokenSeparators = ['.', '-', '_'];

    private static readonly (string Lookalike, string Original)[] LookalikeSequences = [("rn", "m"), ("vv", "w")];
    private static readonly IReadOnlyDictionary<char, char> LookalikeCharacters = new Dictionary<char, char>
    {
        ['0'] = 'o', ['1'] = 'l', ['i'] = 'l', ['3'] = 'e', ['4'] = 'a', ['5'] = 's',
    };

    private static readonly IReadOnlyList<(string Keyword, string Skeleton)> KeywordSkeletons =
        BrandKeywords.All.Select(keyword => (keyword, Skeleton(keyword))).ToList();

    public static bool IsImpersonating(string host, Func<string, bool> isOfficialHost)
    {
        if (isOfficialHost(host))
            return false;

        return host.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(Skeleton)
            .Any(tokenSkeleton => KeywordSkeletons.Any(keyword => Matches(tokenSkeleton, keyword.Keyword, keyword.Skeleton)));
    }

    public static string Skeleton(string token)
    {
        var skeleton = token.ToLowerInvariant();
        foreach (var (lookalike, original) in LookalikeSequences)
            skeleton = skeleton.Replace(lookalike, original, StringComparison.Ordinal);
        return string.Concat(skeleton.Select(character => LookalikeCharacters.GetValueOrDefault(character, character)));
    }

    private static bool Matches(string tokenSkeleton, string keyword, string keywordSkeleton)
    {
        if (tokenSkeleton == keywordSkeleton)
            return true;
        if (keyword.Length >= MinimumSubstringKeywordLength && tokenSkeleton.Contains(keywordSkeleton, StringComparison.Ordinal))
            return true;
        return keyword.Length >= MinimumTypoKeywordLength
            && EditDistance.IsWithin(tokenSkeleton, keywordSkeleton, MaximumTypoDistance);
    }
}
