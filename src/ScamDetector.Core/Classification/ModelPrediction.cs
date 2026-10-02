namespace ScamDetector.Core.Classification;

public sealed record ModelPrediction(IReadOnlyDictionary<MessageLabel, double> Probabilities)
{
    public double ProbabilityOf(MessageLabel label) =>
        Probabilities.TryGetValue(label, out var probability) ? probability : 0;
}
