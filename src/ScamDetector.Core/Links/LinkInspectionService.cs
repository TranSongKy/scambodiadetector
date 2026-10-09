using ScamDetector.Core.Classification;
using ScamDetector.Core.Common;
using ScamDetector.Core.Text;
using ScamDetector.Core.ThreatIntel;
using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Links;

public sealed class LinkInspectionService(
    IUrlInspector urlInspector,
    ClassificationOptions options,
    IThreatIntelligence? threatIntelligence = null) : ILinkInspectionService
{
    private readonly IThreatIntelligence _threatIntelligence = threatIntelligence ?? ThreatIntelligenceIndex.Empty;

    public async Task<Result<IReadOnlyList<LinkInspection>>> InspectAsync(
        IReadOnlyList<string> urls,
        CancellationToken cancellationToken)
    {
        var error = Validate(urls);
        if (error is not null)
            return Result.Failure<IReadOnlyList<LinkInspection>>(error);

        var inspections = new List<LinkInspection>(urls.Count);
        foreach (var url in urls)
            inspections.Add(await InspectLinkAsync(url, cancellationToken));
        return Result.Success<IReadOnlyList<LinkInspection>>(inspections);
    }

    private async Task<LinkInspection> InspectLinkAsync(string url, CancellationToken cancellationToken)
    {
        var normalizedUrl = TextNormalizer.Normalize(url);
        var inspectedFindings = await urlInspector.InspectAsync(normalizedUrl, cancellationToken);
        var threat = _threatIntelligence.Match(normalizedUrl, string.Empty);
        IReadOnlyList<UrlFinding> findings = [.. inspectedFindings, .. threat.BrandImpersonations];
        var reasons = threat.Reasons()
            .Concat(findings.Select(finding => finding.Reason))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new LinkInspection(url, VerdictFor(threat, findings), reasons);
    }

    private LinkVerdict VerdictFor(ThreatMatch threat, IReadOnlyList<UrlFinding> findings)
    {
        if (threat.HasBlocklistedDomain || ClassificationOptions.ReachesThreshold(UrlRisk.Combine(findings), options.ScamThreshold))
            return LinkVerdict.Dangerous;
        return findings.Count > 0 ? LinkVerdict.Suspicious : LinkVerdict.Safe;
    }

    private static DomainError? Validate(IReadOnlyList<string> urls)
    {
        if (urls.Count == 0)
            return LinkInspectionErrors.NoLinks;
        if (urls.Count > LinkInspectionLimits.MaxLinksPerRequest)
            return LinkInspectionErrors.TooManyLinks;
        return urls.Any(url => string.IsNullOrWhiteSpace(url) || url.Length > LinkInspectionLimits.MaxLinkLength)
            ? LinkInspectionErrors.InvalidLink
            : null;
    }
}
