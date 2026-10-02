namespace ScamDetector.Api.Tests.Support;

public static class FixtureModelSettings
{
    private const string FixtureDirectory = "Fixtures";

    public static IReadOnlyDictionary<string, string?> Create() => new Dictionary<string, string?>
    {
        ["OnnxModel:ModelPath"] = FixturePath("fixture-model.onnx"),
        ["OnnxModel:VocabularyPath"] = FixturePath("vocab.txt"),
        ["OnnxModel:BpeCodesPath"] = FixturePath("bpe.codes"),
    };

    public static IReadOnlyDictionary<string, string?> Missing() => new Dictionary<string, string?>
    {
        ["OnnxModel:ModelPath"] = FixturePath("missing.onnx"),
    };

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, FixtureDirectory, fileName);
}
