using ScamDetector.Core.Common;
using ScamDetector.Core.Text;
using ScamDetector.Core.ThreatIntel;
using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Classification;

public sealed class MessageClassifier(
    IScamModel model,
    IUrlInspector urlInspector,
    ClassificationOptions options,
    IThreatIntelligence? threatIntelligence = null) : IMessageClassifier
{
    private readonly IThreatIntelligence _threatIntelligence = threatIntelligence ?? ThreatIntelligenceIndex.Empty;

    public async Task<Result<ClassificationResult>> ClassifyAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Result.Failure<ClassificationResult>(ClassificationErrors.EmptyText);

        var normalizedText = TextNormalizer.Normalize(text);
        if (normalizedText.Length > ClassificationLimits.MaxMessageLength)
            return Result.Failure<ClassificationResult>(ClassificationErrors.TextTooLong);

        var maskedText = ModelInputMasker.Mask(normalizedText);
        var urlFindings = await urlInspector.InspectAsync(normalizedText, cancellationToken);
        var threat = _threatIntelligence.Match(normalizedText, maskedText);
        if (!model.IsAvailable && threat.IsStrongSignal)
            return ClassificationResult.FromThreatSignals(threat, urlFindings);

        var prediction = await model.PredictAsync(maskedText, cancellationToken);
        return ClassificationResult.From(prediction, urlFindings, threat, options.ScamThreshold);
    }
}
