namespace ScamDetector.Core.Classification;

public sealed record ClassificationOptions
{
    public const string SectionName = "Classification";

    public double ScamThreshold { get; init; } = 0.7;
}
