namespace ScamDetector.Infrastructure.Tokenization;

public sealed class PhoBertTokenizer(PhoBertVocabulary vocabulary, BpeWordEncoder wordEncoder)
{
    private static readonly char[] WordSeparators = [' ', '\t', '\n', '\r'];

    public IReadOnlyList<long> Encode(string text, int maxSequenceLength)
    {
        var maxContentTokens = maxSequenceLength - SpecialTokens.SequenceMarkerCount;
        var contentIds = text.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(wordEncoder.Encode)
            .Select(subword => (long)vocabulary.IdOf(subword))
            .Take(maxContentTokens);

        return [SpecialTokens.BeginOfSequenceId, .. contentIds, SpecialTokens.EndOfSequenceId];
    }
}
