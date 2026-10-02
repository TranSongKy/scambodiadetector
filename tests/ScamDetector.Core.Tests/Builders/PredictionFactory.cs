using ScamDetector.Core.Classification;

namespace ScamDetector.Core.Tests.Builders;

public static class PredictionFactory
{
    public static ModelPrediction Create(double normal, double spam, double scam) =>
        new(new Dictionary<MessageLabel, double>
        {
            [MessageLabel.Normal] = normal,
            [MessageLabel.Spam] = spam,
            [MessageLabel.Scam] = scam,
        });

    public static ModelPrediction Empty() => new(new Dictionary<MessageLabel, double>());
}
