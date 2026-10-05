using ScamDetector.Core.Classification;

namespace ScamDetector.Core.Tests.Fakes;

public sealed class FakeReadinessScamModel(bool isAvailable, bool isReady) : IScamModel
{
    public bool IsAvailable => isAvailable;

    public int ReadinessCallCount { get; private set; }

    public CancellationToken ReceivedReadinessToken { get; private set; }

    public Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        ReadinessCallCount++;
        ReceivedReadinessToken = cancellationToken;
        return Task.FromResult(isReady);
    }

    public Task<ModelPrediction> PredictAsync(string maskedText, CancellationToken cancellationToken) =>
        Task.FromResult(new ModelPrediction(new Dictionary<MessageLabel, double>()));
}
