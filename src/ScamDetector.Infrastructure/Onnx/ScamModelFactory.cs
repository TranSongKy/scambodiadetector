using Microsoft.ML.OnnxRuntime;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.Tokenization;

namespace ScamDetector.Infrastructure.Onnx;

public static class ScamModelFactory
{
    public static IScamModel Create(OnnxModelOptions options, string contentRootPath)
    {
        var modelPath = Path.Combine(contentRootPath, options.ModelPath);
        var vocabularyPath = Path.Combine(contentRootPath, options.VocabularyPath);
        var bpeCodesPath = Path.Combine(contentRootPath, options.BpeCodesPath);

        var missingFiles = new[] { modelPath, vocabularyPath, bpeCodesPath }.Where(path => !File.Exists(path)).ToList();
        if (missingFiles.Count > 0)
            return new UnavailableScamModel(missingFiles);

        var tokenizer = new PhoBertTokenizer(
            PhoBertVocabulary.Load(vocabularyPath),
            new BpeWordEncoder(BpeMergeRanks.Load(bpeCodesPath)));
        return new OnnxScamModel(new InferenceSession(modelPath), tokenizer, options);
    }
}
