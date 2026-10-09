using ScamDetector.Core.Urls;

namespace ScamDetector.Core.ThreatIntel;

public sealed record ThreatMatch(IReadOnlyList<string> BlocklistedDomains, TemplateMatch? Template)
{
    public static readonly ThreatMatch None = new([], null);

    public const double BlocklistedDomainConfidence = 0.99;

    public IReadOnlyList<UrlFinding> BrandImpersonations { get; init; } = [];

    public bool HasBlocklistedDomain => BlocklistedDomains.Count > 0;

    public bool IsStrongSignal => HasBlocklistedDomain || Template is not null;

    public double SignalConfidence => HasBlocklistedDomain ? BlocklistedDomainConfidence : Template?.Coverage ?? 0;

    public IEnumerable<string> Reasons()
    {
        if (HasBlocklistedDomain)
            yield return ThreatReasons.BlocklistedDomain;
        if (Template is not null)
            yield return ThreatReasons.KnownScamTemplate;
    }
}
