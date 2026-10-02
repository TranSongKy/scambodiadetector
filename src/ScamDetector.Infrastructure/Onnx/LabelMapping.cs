using ScamDetector.Core.Classification;

namespace ScamDetector.Infrastructure.Onnx;

public static class LabelMapping
{
    public static IReadOnlyList<MessageLabel> Parse(IReadOnlyList<string> labelOrder) =>
        labelOrder.Select(label => Enum.Parse<MessageLabel>(label, ignoreCase: true)).ToList();

    public static ModelPrediction ToPrediction(IReadOnlyList<MessageLabel> labels, IReadOnlyList<float> logits)
    {
        if (logits.Count != labels.Count)
            throw new InvalidOperationException($"Model returned {logits.Count} logits but {labels.Count} labels are configured.");

        var probabilities = Softmax(logits);
        return new ModelPrediction(labels.Zip(probabilities).ToDictionary(pair => pair.First, pair => pair.Second));
    }

    private static double[] Softmax(IReadOnlyList<float> logits)
    {
        var maxLogit = logits.Max();
        var exponents = logits.Select(logit => Math.Exp(logit - maxLogit)).ToArray();
        var sum = exponents.Sum();
        return exponents.Select(exponent => exponent / sum).ToArray();
    }
}
