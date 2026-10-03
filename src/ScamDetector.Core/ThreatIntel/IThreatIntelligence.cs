namespace ScamDetector.Core.ThreatIntel;

public interface IThreatIntelligence
{
    ThreatMatch Match(string normalizedText, string maskedText);
}
