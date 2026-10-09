using ScamDetector.Core.Urls;

namespace ScamDetector.Core.ThreatIntel;

public sealed class ThreatIntelligenceIndex : IThreatIntelligence
{
    public const double DefaultMinimumCoverage = 0.6;
    private const char LabelSeparator = '.';

    private readonly HashSet<string> _blockedDomains;
    private readonly HashSet<string> _officialDomains;
    private readonly List<(string Id, IReadOnlySet<string> Shingles)> _templates;
    private readonly Dictionary<string, List<int>> _templatesByShingle = new(StringComparer.Ordinal);
    private readonly double _minimumCoverage;

    public ThreatIntelligenceIndex(
        IEnumerable<string> blockedDomains,
        IEnumerable<ScamTemplateEntry> templates,
        double minimumCoverage = DefaultMinimumCoverage,
        IEnumerable<string>? officialDomains = null)
    {
        _blockedDomains = ToDomainSet(blockedDomains);
        _officialDomains = ToDomainSet(officialDomains ?? []);
        _templates = templates.Select(template => (template.Id, TemplateShingles.Build(template.Text))).ToList();
        _minimumCoverage = minimumCoverage;
        IndexShingles();
    }

    public static ThreatIntelligenceIndex Empty { get; } = new([], []);

    public int BlockedDomainCount => _blockedDomains.Count;

    public int TemplateCount => _templates.Count;

    public int OfficialDomainCount => _officialDomains.Count;

    public ThreatMatch Match(string normalizedText, string maskedText)
    {
        var links = UrlExtractor.Extract(normalizedText)
            .Select(url => (Url: url, Host: UrlHost.Parse(url)))
            .Where(link => link.Host is not null)
            .Select(link => (link.Url, Host: link.Host!))
            .ToList();
        var blockedDomains = links
            .Select(link => link.Host)
            .Where(IsBlocked)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var brandImpersonations = links
            .Where(link => !IsBlocked(link.Host) && BrandImpersonation.IsImpersonating(link.Host, IsOfficial))
            .Select(link => new UrlFinding(link.Url, UrlReasons.BrandImpersonation))
            .Distinct()
            .ToList();
        return new ThreatMatch(blockedDomains, BestTemplate(maskedText)) { BrandImpersonations = brandImpersonations };
    }

    public bool IsBlocked(string host) => MatchesDomainOrParent(host, _blockedDomains);

    public bool IsOfficial(string host) => MatchesDomainOrParent(host, _officialDomains);

    private static bool MatchesDomainOrParent(string host, HashSet<string> domains)
    {
        var labels = host.TrimEnd(LabelSeparator).Split(LabelSeparator);
        return Enumerable.Range(0, labels.Length - 1)
            .Any(start => domains.Contains(string.Join(LabelSeparator, labels[start..])));
    }

    private static HashSet<string> ToDomainSet(IEnumerable<string> domains) =>
        domains.Select(domain => domain.Trim().ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

    private void IndexShingles()
    {
        _templatesByShingle.Clear();
        for (var index = 0; index < _templates.Count; index++)
        {
            foreach (var shingle in _templates[index].Shingles)
            {
                if (!_templatesByShingle.TryGetValue(shingle, out var templateIndexes))
                    _templatesByShingle[shingle] = templateIndexes = [];
                templateIndexes.Add(index);
            }
        }
    }

    private TemplateMatch? BestTemplate(string maskedText)
    {
        var messageShingles = TemplateShingles.Build(maskedText);
        var candidates = messageShingles
            .SelectMany(shingle => _templatesByShingle.GetValueOrDefault(shingle) ?? [])
            .Distinct();
        return candidates
            .Select(index => new TemplateMatch(_templates[index].Id, TemplateShingles.Coverage(_templates[index].Shingles, messageShingles)))
            .Where(match => match.Coverage >= _minimumCoverage)
            .OrderByDescending(match => match.Coverage)
            .ThenBy(match => match.TemplateId, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
