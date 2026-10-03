using ScamDetector.Core.ThreatIntel;

namespace ScamDetector.Core.Tests.Fakes;

public sealed class FakeThreatIntelligence(ThreatMatch match) : IThreatIntelligence
{
    public int CallCount { get; private set; }

    public string? ReceivedNormalizedText { get; private set; }

    public string? ReceivedMaskedText { get; private set; }

    public ThreatMatch Match(string normalizedText, string maskedText)
    {
        CallCount++;
        ReceivedNormalizedText = normalizedText;
        ReceivedMaskedText = maskedText;
        return match;
    }
}
