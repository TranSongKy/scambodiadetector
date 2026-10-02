namespace ScamDetector.Infrastructure.Tests.Support;

public static class FixturePaths
{
    public const string Directory = "Fixtures";
    public const string ModelFileName = "fixture-model.onnx";
    public const string VocabularyFileName = "vocab.txt";
    public const string BpeCodesFileName = "bpe.codes";

    public static string Root => Path.Combine(AppContext.BaseDirectory, Directory);
}
