using ScamDetector.Core.ThreatIntel;
using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Classification;

public sealed record ClassificationResult(MessageLabel Label, double Confidence, IReadOnlyList<string> Reasons)
{
    public static ClassificationResult From(
        ModelPrediction prediction,
        IReadOnlyList<UrlFinding> urlFindings,
        double scamThreshold) =>
        From(prediction, urlFindings, ThreatMatch.None, scamThreshold);

    public static ClassificationResult From(
        ModelPrediction prediction,
        IReadOnlyList<UrlFinding> urlFindings,
        ThreatMatch threat,
        double scamThreshold)
    {
        var modelScamProbability = prediction.ProbabilityOf(MessageLabel.Scam);
        var scamProbability = UrlRisk.CombineWithModel(modelScamProbability, UrlRisk.Combine(urlFindings));
        var reasons = new List<string>();

        if (scamProbability >= scamThreshold || threat.IsStrongSignal)
        {
            if (modelScamProbability >= scamThreshold)
                reasons.Add(ClassificationReasons.ModelPredictedScam);
            reasons.AddRange(threat.Reasons());
            reasons.AddRange(urlFindings.Select(finding => finding.Reason));
            return new ClassificationResult(MessageLabel.Scam, Math.Max(scamProbability, threat.SignalConfidence), reasons);
        }

        var label = PickNonScamLabel(prediction);
        if (label == MessageLabel.Spam)
            reasons.Add(ClassificationReasons.ModelPredictedSpam);
        if (scamProbability > prediction.ProbabilityOf(label))
            reasons.Add(ClassificationReasons.ScamBelowThreshold);
        reasons.AddRange(urlFindings.Select(finding => finding.Reason));

        return new ClassificationResult(label, prediction.ProbabilityOf(label), reasons);
    }

    public static ClassificationResult FromUrlSignals(IReadOnlyList<UrlFinding> urlFindings) =>
        new(MessageLabel.Scam, UrlRisk.Combine(urlFindings), [.. urlFindings.Select(finding => finding.Reason)]);

    public static ClassificationResult FromThreatSignals(ThreatMatch threat, IReadOnlyList<UrlFinding> urlFindings) =>
        new(
            MessageLabel.Scam,
            threat.SignalConfidence,
            [.. threat.Reasons(), .. urlFindings.Select(finding => finding.Reason)]);

    private static MessageLabel PickNonScamLabel(ModelPrediction prediction) =>
        prediction.ProbabilityOf(MessageLabel.Spam) > prediction.ProbabilityOf(MessageLabel.Normal)
            ? MessageLabel.Spam
            : MessageLabel.Normal;
}
