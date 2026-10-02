namespace ScamDetector.Api.RateLimiting;

public sealed record RateLimitOptions
{
    public const string SectionName = "RateLimiting";
    public const string PolicyName = "per-client";

    public const int DefaultPermitLimit = 30;
    public const int DefaultWindowSeconds = 60;

    public int PermitLimit { get; init; } = DefaultPermitLimit;

    public int WindowSeconds { get; init; } = DefaultWindowSeconds;

    public bool TrustForwardedHeaders { get; init; }
}
