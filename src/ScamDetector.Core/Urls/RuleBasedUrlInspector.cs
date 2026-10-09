using System.Net;

namespace ScamDetector.Core.Urls;

public sealed class RuleBasedUrlInspector : IUrlInspector
{
    public Task<IReadOnlyList<UrlFinding>> InspectAsync(string normalizedText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var literalUrls = UrlExtractor.ExtractLiteral(normalizedText).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<UrlFinding> findings = UrlExtractor.Extract(normalizedText)
            .SelectMany(url => InspectUrl(url, isObfuscated: !literalUrls.Contains(url)))
            .ToList();
        return Task.FromResult(findings);
    }

    private static IEnumerable<UrlFinding> InspectUrl(string url, bool isObfuscated)
    {
        if (isObfuscated)
            yield return new UrlFinding(url, UrlReasons.Obfuscated);

        var host = UrlHost.Parse(url);
        if (host is null)
            yield break;

        if (UrlRuleLists.ShortenerHosts.Contains(host))
            yield return new UrlFinding(url, UrlReasons.Shortener);
        if (IPAddress.TryParse(host, out _))
            yield return new UrlFinding(url, UrlReasons.IpAddressHost);
        if (host.Split('.').Any(label => label.StartsWith(UrlRuleLists.PunycodePrefix, StringComparison.Ordinal)))
            yield return new UrlFinding(url, UrlReasons.Punycode);
        if (UrlRuleLists.SuspiciousTopLevelDomains.Contains(host[(host.LastIndexOf('.') + 1)..]))
            yield return new UrlFinding(url, UrlReasons.SuspiciousTopLevelDomain);
    }
}
