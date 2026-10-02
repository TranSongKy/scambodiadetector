namespace ScamDetector.Core.Classification;

public static class ClassificationReasons
{
    public const string ModelPredictedScam = "model_predicted_scam";
    public const string ModelPredictedSpam = "model_predicted_spam";
    public const string ScamBelowThreshold = "scam_probability_below_threshold";
}
