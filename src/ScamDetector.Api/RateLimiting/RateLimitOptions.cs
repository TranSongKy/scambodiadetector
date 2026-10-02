namespace ScamDetector.Api.RateLimiting;

public sealed record RateLimitOptions
{
    public const string SectionName = "RateLimiting";
    public const string PolicyName = "per-client";

    public int PermitLimit { get; init; } = 30;

    public int WindowSeconds { get; init; } = 60;

    public bool TrustForwardedHeaders { get; init; }
}
