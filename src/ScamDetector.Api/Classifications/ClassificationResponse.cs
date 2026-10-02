namespace ScamDetector.Api.Classifications;

public sealed record ClassificationResponse(string Label, double Confidence, IReadOnlyList<string> Reasons);
