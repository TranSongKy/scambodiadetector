namespace ScamDetector.Infrastructure.Tokenization;

public sealed class PhoBertVocabulary(IReadOnlyDictionary<string, int> tokenIds)
{
    public int IdOf(string token) =>
        tokenIds.TryGetValue(token, out var id) ? id : SpecialTokens.UnknownId;

    public static PhoBertVocabulary FromLines(IEnumerable<string> lines)
    {
        var tokenIds = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in lines.Select(FirstField).Where(token => token.Length > 0))
            tokenIds.TryAdd(token, SpecialTokens.FirstVocabularyId + tokenIds.Count);
        return new PhoBertVocabulary(tokenIds);
    }

    public static PhoBertVocabulary Load(string path) => FromLines(File.ReadLines(path));

    private static string FirstField(string line)
    {
        var separatorIndex = line.LastIndexOf(' ');
        return separatorIndex < 0 ? line.Trim() : line[..separatorIndex];
    }
}
