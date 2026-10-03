namespace ScamDetector.Infrastructure.ThreatIntel;

public sealed record ThreatIntelOptions
{
    public const string SectionName = "ThreatIntel";

    public string DataDirectory { get; init; } = "../../data/threat-intel";

    public double MinimumTemplateCoverage { get; init; } = Core.ThreatIntel.ThreatIntelligenceIndex.DefaultMinimumCoverage;

    public int ReloadCheckSeconds { get; init; } = 60;
}
