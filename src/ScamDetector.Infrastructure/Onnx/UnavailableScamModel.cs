using ScamDetector.Core.Classification;

namespace ScamDetector.Infrastructure.Onnx;

public sealed class UnavailableScamModel(IReadOnlyList<string> missingFiles) : IScamModel
{
    public IReadOnlyList<string> MissingFiles => missingFiles;

    public Task<ModelPrediction> PredictAsync(string maskedText, CancellationToken cancellationToken) =>
        throw new ScamModelUnavailableException($"Model files not found: {string.Join(", ", missingFiles)}");
}
