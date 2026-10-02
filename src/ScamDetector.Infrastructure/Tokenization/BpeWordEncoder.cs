namespace ScamDetector.Infrastructure.Tokenization;

public sealed class BpeWordEncoder(BpeMergeRanks mergeRanks)
{
    public IReadOnlyList<string> Encode(string word)
    {
        var symbols = SplitIntoSymbols(word);
        while (symbols.Count > 1 && FindBestMerge(symbols) is { } mergeIndex)
        {
            symbols[mergeIndex] += symbols[mergeIndex + 1];
            symbols.RemoveAt(mergeIndex + 1);
        }
        return ToSubwords(symbols);
    }

    private static List<string> SplitIntoSymbols(string word)
    {
        var symbols = word.EnumerateRunes().Select(rune => rune.ToString()).ToList();
        symbols[^1] += BpeSymbols.EndOfWord;
        return symbols;
    }

    private int? FindBestMerge(List<string> symbols)
    {
        int? bestIndex = null;
        var bestRank = int.MaxValue;
        for (var index = 0; index < symbols.Count - 1; index++)
        {
            if (mergeRanks.RankOf(symbols[index], symbols[index + 1]) is { } rank && rank < bestRank)
            {
                bestRank = rank;
                bestIndex = index;
            }
        }
        return bestIndex;
    }

    private static List<string> ToSubwords(List<string> symbols)
    {
        var subwords = symbols.Select(symbol => symbol + BpeSymbols.Continuation).ToList();
        subwords[^1] = symbols[^1][..^BpeSymbols.EndOfWord.Length];
        return subwords;
    }
}
