using ScamDetector.Core.Common;
using ScamDetector.Core.Text;
using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Classification;

public sealed class MessageClassifier(
    IScamModel model,
    IUrlInspector urlInspector,
    ClassificationOptions options) : IMessageClassifier
{
    public async Task<Result<ClassificationResult>> ClassifyAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Result.Failure<ClassificationResult>(ClassificationErrors.EmptyText);

        var normalizedText = TextNormalizer.Normalize(text);
        if (normalizedText.Length > ClassificationLimits.MaxMessageLength)
            return Result.Failure<ClassificationResult>(ClassificationErrors.TextTooLong);

        var prediction = await model.PredictAsync(ModelInputMasker.Mask(normalizedText), cancellationToken);
        var urlFindings = await urlInspector.InspectAsync(normalizedText, cancellationToken);

        return ClassificationResult.From(prediction, urlFindings, options.ScamThreshold);
    }
}
