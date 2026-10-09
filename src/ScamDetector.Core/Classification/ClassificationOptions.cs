namespace ScamDetector.Core.Classification;

public sealed record ClassificationOptions
{
    public const string SectionName = "Classification";

    public const double DefaultScamThreshold = 0.7;

    public const double ThresholdTolerance = 1e-9;

    public double ScamThreshold { get; init; } = DefaultScamThreshold;

    public static bool ReachesThreshold(double probability, double threshold) =>
        probability >= threshold - ThresholdTolerance;
}
