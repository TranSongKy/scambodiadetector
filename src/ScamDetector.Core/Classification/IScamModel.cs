namespace ScamDetector.Core.Classification;

public interface IScamModel
{
    bool IsAvailable => true;

    Task<bool> IsReadyAsync(CancellationToken cancellationToken) => Task.FromResult(IsAvailable);

    Task<ModelPrediction> PredictAsync(string maskedText, CancellationToken cancellationToken);
}
