using ScamDetector.Infrastructure.Tokenization;

namespace ScamDetector.Infrastructure.Tests.Support;

public static class FixtureTokenizer
{
    public static PhoBertTokenizer Create() =>
        new(
            PhoBertVocabulary.Load(Path.Combine(FixturePaths.Root, FixturePaths.VocabularyFileName)),
            new BpeWordEncoder(BpeMergeRanks.Load(Path.Combine(FixturePaths.Root, FixturePaths.BpeCodesFileName))));
}
