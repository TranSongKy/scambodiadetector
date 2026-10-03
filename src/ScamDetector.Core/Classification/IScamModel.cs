namespace ScamDetector.Core.Classification;

public interface IScamModel
{
    bool IsAvailable => true;

    Task<ModelPrediction> PredictAsync(string maskedText, CancellationToken cancellationToken);
}
