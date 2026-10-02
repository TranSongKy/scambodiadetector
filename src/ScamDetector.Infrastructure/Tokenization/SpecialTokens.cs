namespace ScamDetector.Infrastructure.Tokenization;

public static class SpecialTokens
{
    public const int BeginOfSequenceId = 0;
    public const int PaddingId = 1;
    public const int EndOfSequenceId = 2;
    public const int UnknownId = 3;
    public const int FirstVocabularyId = 4;
    public const int SequenceMarkerCount = 2;
}
