using ScamDetector.Core.ThreatIntel;

namespace ScamDetector.Core.Tests.ThreatIntel;

public sealed class ThreatMatchTests
{
    private const string Domain = "bad.example";
    private const double TemplateCoverage = 0.75;

    private static readonly TemplateMatch Template = new("t1", TemplateCoverage);

    [Fact]
    public void None_HasNoSignal()
    {
        var match = ThreatMatch.None;

        Assert.False(match.IsStrongSignal);
        Assert.False(match.HasBlocklistedDomain);
        Assert.Equal(0, match.SignalConfidence);
        Assert.Empty(match.Reasons());
    }

    [Fact]
    public void IsStrongSignal_OnlyBlocklistedDomain_ReturnsTrue()
    {
        var match = new ThreatMatch([Domain], null);

        Assert.True(match.IsStrongSignal);
    }

    [Fact]
    public void IsStrongSignal_OnlyTemplate_ReturnsTrue()
    {
        var match = new ThreatMatch([], Template);

        Assert.True(match.IsStrongSignal);
    }

    [Fact]
    public void SignalConfidence_OnlyBlocklistedDomain_ReturnsBlocklistConfidence()
    {
        var match = new ThreatMatch([Domain], null);

        Assert.Equal(ThreatMatch.BlocklistedDomainConfidence, match.SignalConfidence);
    }

    [Fact]
    public void SignalConfidence_OnlyTemplate_ReturnsTemplateCoverage()
    {
        var match = new ThreatMatch([], Template);

        Assert.Equal(TemplateCoverage, match.SignalConfidence);
    }

    [Fact]
    public void SignalConfidence_DomainAndTemplate_ReturnsBlocklistConfidence()
    {
        var match = new ThreatMatch([Domain], new TemplateMatch("t1", 1.0));

        Assert.Equal(ThreatMatch.BlocklistedDomainConfidence, match.SignalConfidence);
    }

    [Fact]
    public void Reasons_OnlyBlocklistedDomain_ReturnsBlocklistedReason()
    {
        var match = new ThreatMatch([Domain], null);

        Assert.Equal([ThreatReasons.BlocklistedDomain], match.Reasons());
    }

    [Fact]
    public void Reasons_OnlyTemplate_ReturnsTemplateReason()
    {
        var match = new ThreatMatch([], Template);

        Assert.Equal([ThreatReasons.KnownScamTemplate], match.Reasons());
    }

    [Fact]
    public void Reasons_DomainAndTemplate_ReturnsBothInOrder()
    {
        var match = new ThreatMatch([Domain], Template);

        Assert.Equal([ThreatReasons.BlocklistedDomain, ThreatReasons.KnownScamTemplate], match.Reasons());
    }
}
