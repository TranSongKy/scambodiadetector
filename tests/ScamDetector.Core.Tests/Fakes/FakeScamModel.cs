using ScamDetector.Core.Classification;

namespace ScamDetector.Core.Tests.Fakes;

public sealed class FakeScamModel(ModelPrediction prediction) : IScamModel
{
    public int CallCount { get; private set; }

    public string? ReceivedText { get; private set; }

    public CancellationToken ReceivedToken { get; private set; }

    public Task<ModelPrediction> PredictAsync(string maskedText, CancellationToken cancellationToken)
    {
        CallCount++;
        ReceivedText = maskedText;
        ReceivedToken = cancellationToken;
        return Task.FromResult(prediction);
    }
}
