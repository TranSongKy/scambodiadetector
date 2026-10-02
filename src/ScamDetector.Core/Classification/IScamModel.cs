namespace ScamDetector.Core.Classification;

public interface IScamModel
{
    Task<ModelPrediction> PredictAsync(string maskedText, CancellationToken cancellationToken);
}
