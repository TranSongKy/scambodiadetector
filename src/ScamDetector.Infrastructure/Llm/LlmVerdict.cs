using System.Text.Json;
using ScamDetector.Core.Classification;

namespace ScamDetector.Infrastructure.Llm;

public sealed record LlmVerdict(MessageLabel Label, double Confidence)
{
    public const double MinimumChosenLabelConfidence = 0.5;

    public static LlmVerdict? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var payload = JsonSerializer.Deserialize<LlmVerdictPayload>(json, OllamaJson.Options);
        return payload is { Confidence: { } confidence } && MessageLabelNames.TryParse(payload.Label, out var label)
            ? new LlmVerdict(label, Math.Clamp(confidence, MinimumChosenLabelConfidence, 1))
            : null;
    }

    public ModelPrediction ToPrediction()
    {
        var labels = Enum.GetValues<MessageLabel>();
        var remainingShare = (1 - Confidence) / (labels.Length - 1);
        return new ModelPrediction(labels.ToDictionary(
            label => label,
            label => label == Label ? Confidence : remainingShare));
    }
}
