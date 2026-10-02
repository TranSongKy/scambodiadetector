using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Classification;

public sealed record ClassificationResult(MessageLabel Label, double Confidence, IReadOnlyList<string> Reasons)
{
    public static ClassificationResult From(
        ModelPrediction prediction,
        IReadOnlyList<UrlFinding> urlFindings,
        double scamThreshold)
    {
        var scamProbability = prediction.ProbabilityOf(MessageLabel.Scam);
        var reasons = new List<string>();

        if (scamProbability >= scamThreshold)
        {
            reasons.Add(ClassificationReasons.ModelPredictedScam);
            reasons.AddRange(urlFindings.Select(finding => finding.Reason));
            return new ClassificationResult(MessageLabel.Scam, scamProbability, reasons);
        }

        var label = PickNonScamLabel(prediction);
        if (label == MessageLabel.Spam)
            reasons.Add(ClassificationReasons.ModelPredictedSpam);
        if (scamProbability > prediction.ProbabilityOf(label))
            reasons.Add(ClassificationReasons.ScamBelowThreshold);
        reasons.AddRange(urlFindings.Select(finding => finding.Reason));

        return new ClassificationResult(label, prediction.ProbabilityOf(label), reasons);
    }

    private static MessageLabel PickNonScamLabel(ModelPrediction prediction) =>
        prediction.ProbabilityOf(MessageLabel.Spam) > prediction.ProbabilityOf(MessageLabel.Normal)
            ? MessageLabel.Spam
            : MessageLabel.Normal;
}
