namespace ScamDetector.Core.Classification;

public sealed record ClassificationOptions
{
    public const string SectionName = "Classification";

    public const double DefaultScamThreshold = 0.7;

    public double ScamThreshold { get; init; } = DefaultScamThreshold;
}
