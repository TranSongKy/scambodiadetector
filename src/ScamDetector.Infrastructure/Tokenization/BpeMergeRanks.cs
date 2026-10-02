namespace ScamDetector.Infrastructure.Tokenization;

public sealed class BpeMergeRanks(IReadOnlyDictionary<(string Left, string Right), int> ranks)
{
    public int? RankOf(string left, string right) =>
        ranks.TryGetValue((left, right), out var rank) ? rank : null;

    public static BpeMergeRanks FromLines(IEnumerable<string> lines)
    {
        var ranks = new Dictionary<(string Left, string Right), int>();
        foreach (var fields in lines.Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
        {
            if (fields.Length >= 2)
                ranks.TryAdd((fields[0], fields[1]), ranks.Count);
        }
        return new BpeMergeRanks(ranks);
    }

    public static BpeMergeRanks Load(string path) => FromLines(File.ReadLines(path));
}
