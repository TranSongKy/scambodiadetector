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

        if (ClassificationOptions.ReachesThreshold(scamProbability, scamThreshold) || threat.IsStrongSignal)
        {
            if (ClassificationOptions.ReachesThreshold(modelScamProbability, scamThreshold))
                reasons.Add(ClassificationReasons.ModelPredictedScam);
            reasons.AddRange(threat.Reasons());
            reasons.AddRange(UrlReasonsOf(urlFindings));
            return new ClassificationResult(MessageLabel.Scam, Math.Max(scamProbability, threat.SignalConfidence), Distinct(reasons));
        }

        var label = PickNonScamLabel(prediction);
        if (label == MessageLabel.Spam)
            reasons.Add(ClassificationReasons.ModelPredictedSpam);
        if (scamProbability > prediction.ProbabilityOf(label))
            reasons.Add(ClassificationReasons.ScamBelowThreshold);
        reasons.AddRange(UrlReasonsOf(urlFindings));

        return new ClassificationResult(label, prediction.ProbabilityOf(label), Distinct(reasons));
    }

    public static ClassificationResult FromUrlSignals(IReadOnlyList<UrlFinding> urlFindings) =>
        new(MessageLabel.Scam, UrlRisk.Combine(urlFindings), Distinct(UrlReasonsOf(urlFindings)));

    public static ClassificationResult FromThreatSignals(ThreatMatch threat, IReadOnlyList<UrlFinding> urlFindings) =>
        new(
            MessageLabel.Scam,
            threat.SignalConfidence,
            Distinct([.. threat.Reasons(), .. UrlReasonsOf(urlFindings)]));

    private static IEnumerable<string> UrlReasonsOf(IReadOnlyList<UrlFinding> urlFindings) =>
        urlFindings.Select(finding => finding.Reason);

    private static List<string> Distinct(IEnumerable<string> reasons) =>
        reasons.Distinct(StringComparer.Ordinal).ToList();

    private static MessageLabel PickNonScamLabel(ModelPrediction prediction) =>
        prediction.ProbabilityOf(MessageLabel.Spam) > prediction.ProbabilityOf(MessageLabel.Normal)
            ? MessageLabel.Spam
            : MessageLabel.Normal;
}
