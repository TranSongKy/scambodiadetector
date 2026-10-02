using ScamDetector.Infrastructure.Tokenization;

namespace ScamDetector.Infrastructure.Tests.Tokenization;

public sealed class PhoBertVocabularyTests
{
    [Fact]
    public void IdOf_FirstVocabularyLine_ReturnsFirstVocabularyId()
    {
        var vocabulary = PhoBertVocabulary.FromLines(["xin 10", "chào 9"]);

        Assert.Equal(SpecialTokens.FirstVocabularyId, vocabulary.IdOf("xin"));
        Assert.Equal(SpecialTokens.FirstVocabularyId + 1, vocabulary.IdOf("chào"));
    }

    [Fact]
    public void IdOf_UnknownToken_ReturnsUnknownId()
    {
        var vocabulary = PhoBertVocabulary.FromLines(["xin 10"]);

        Assert.Equal(SpecialTokens.UnknownId, vocabulary.IdOf("zzz"));
    }

    [Fact]
    public void FromLines_DuplicateToken_KeepsFirstId()
    {
        var vocabulary = PhoBertVocabulary.FromLines(["xin 10", "xin 5", "chào 9"]);

        Assert.Equal(SpecialTokens.FirstVocabularyId, vocabulary.IdOf("xin"));
        Assert.Equal(SpecialTokens.FirstVocabularyId + 1, vocabulary.IdOf("chào"));
    }

    [Fact]
    public void FromLines_BlankLine_IsSkipped()
    {
        var vocabulary = PhoBertVocabulary.FromLines(["", "xin 10"]);

        Assert.Equal(SpecialTokens.FirstVocabularyId, vocabulary.IdOf("xin"));
    }
}
