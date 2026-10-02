namespace ScamDetector.Core.Classification;

public interface IScamModel
{
    Task<ModelPrediction> PredictAsync(string normalizedText, CancellationToken cancellationToken);
}
