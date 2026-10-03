using ScamDetector.Core.ThreatIntel;

namespace ScamDetector.Infrastructure.ThreatIntel;

public static class ThreatIntelLoader
{
    public static ThreatIntelligenceIndex Load(string dataDirectory, double minimumCoverage)
    {
        var domains = ReadTable(Path.Combine(dataDirectory, ThreatIntelFiles.BlockedDomains))
            .Select(row => row.GetValueOrDefault(ThreatIntelFiles.DomainColumn) ?? string.Empty)
            .Where(domain => domain.Length > 0);
        var templates = ReadTable(Path.Combine(dataDirectory, ThreatIntelFiles.ScamTemplates))
            .Select(row => new ScamTemplateEntry(
                row.GetValueOrDefault(ThreatIntelFiles.IdColumn) ?? string.Empty,
                row.GetValueOrDefault(ThreatIntelFiles.TextColumn) ?? string.Empty))
            .Where(template => template.Text.Length > 0);
        return new ThreatIntelligenceIndex(domains, templates, minimumCoverage);
    }

    public static DateTime LatestWriteTimeUtc(string dataDirectory) =>
        new[] { ThreatIntelFiles.BlockedDomains, ThreatIntelFiles.ScamTemplates }
            .Select(file => Path.Combine(dataDirectory, file))
            .Where(File.Exists)
            .Select(File.GetLastWriteTimeUtc)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadTable(string path) =>
        File.Exists(path) ? CsvTable.Parse(File.ReadAllText(path)) : [];
}
