using ScamDetector.Infrastructure.Onnx;
using ScamDetector.Infrastructure.Tests.Support;

namespace ScamDetector.Infrastructure.Tests.Onnx;

public sealed class ScamModelFactoryTests
{
    private static readonly OnnxModelOptions FixtureOptions = new()
    {
        ModelPath = FixturePaths.ModelFileName,
        VocabularyPath = FixturePaths.VocabularyFileName,
        BpeCodesPath = FixturePaths.BpeCodesFileName,
    };

    [Fact]
    public void Create_AllFilesPresent_ReturnsOnnxScamModel()
    {
        var model = ScamModelFactory.Create(FixtureOptions, FixturePaths.Root);

        using var onnxModel = Assert.IsType<OnnxScamModel>(model);
    }

    [Fact]
    public void Create_ModelFileMissing_ReturnsUnavailableModelListingMissingFile()
    {
        var options = FixtureOptions with { ModelPath = "missing.onnx" };

        var model = ScamModelFactory.Create(options, FixturePaths.Root);

        var unavailableModel = Assert.IsType<UnavailableScamModel>(model);
        Assert.Single(unavailableModel.MissingFiles);
        Assert.EndsWith("missing.onnx", unavailableModel.MissingFiles[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Create_AllFilesMissing_ListsEveryMissingFile()
    {
        var model = ScamModelFactory.Create(FixtureOptions, Path.GetTempPath());

        var unavailableModel = Assert.IsType<UnavailableScamModel>(model);
        Assert.Equal(3, unavailableModel.MissingFiles.Count);
    }
}
