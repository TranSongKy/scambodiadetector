using System.Collections.Frozen;

namespace ScamDetector.Core.Urls;

public static class UrlRisk
{
    public const double BrandImpersonationWeight = 0.6;
    public const double ObfuscatedWeight = 0.4;
    public const double IpAddressHostWeight = 0.4;
    public const double PunycodeWeight = 0.4;
    public const double SuspiciousTopLevelDomainWeight = 0.3;
    public const double ShortenerWeight = 0.15;

    private static readonly FrozenDictionary<string, double> Weights = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        [UrlReasons.BrandImpersonation] = BrandImpersonationWeight,
        [UrlReasons.Obfuscated] = ObfuscatedWeight,
        [UrlReasons.IpAddressHost] = IpAddressHostWeight,
        [UrlReasons.Punycode] = PunycodeWeight,
        [UrlReasons.SuspiciousTopLevelDomain] = SuspiciousTopLevelDomainWeight,
        [UrlReasons.Shortener] = ShortenerWeight,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static double WeightOf(string reason) => Weights.GetValueOrDefault(reason);

    public static double Combine(IEnumerable<UrlFinding> findings) =>
        1 - findings
            .Select(finding => finding.Reason)
            .Distinct(StringComparer.Ordinal)
            .Aggregate(1.0, (remainingTrust, reason) => remainingTrust * (1 - WeightOf(reason)));

    public static double CombineWithModel(double modelScamProbability, double urlRisk) =>
        1 - (1 - modelScamProbability) * (1 - urlRisk);
}
