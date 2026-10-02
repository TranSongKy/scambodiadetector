using ScamDetector.Core.Classification;

namespace ScamDetector.Api.Tests.Fakes;

public sealed class FakeScamModel(ModelPrediction prediction) : IScamModel
{
    public string? ReceivedText { get; private set; }

    public Task<ModelPrediction> PredictAsync(string maskedText, CancellationToken cancellationToken)
    {
        ReceivedText = maskedText;
        return Task.FromResult(prediction);
    }

    public static FakeScamModel Returning(double normal, double spam, double scam) =>
        new(new ModelPrediction(new Dictionary<MessageLabel, double>
        {
            [MessageLabel.Normal] = normal,
            [MessageLabel.Spam] = spam,
            [MessageLabel.Scam] = scam,
        }));
}
