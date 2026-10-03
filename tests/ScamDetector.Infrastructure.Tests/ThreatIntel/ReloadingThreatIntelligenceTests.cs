using ScamDetector.Infrastructure.Tests.Support;
using ScamDetector.Infrastructure.ThreatIntel;

namespace ScamDetector.Infrastructure.Tests.ThreatIntel;

public sealed class ReloadingThreatIntelligenceTests : IDisposable
{
    private const string Message = "Vào https://vcb-xacminh.com ngay";
    private const string MaskedMessage = "Vào <URL> ngay";
    private static readonly DateTime InitialWriteTime = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ThreatIntelOptions Options = new() { ReloadCheckSeconds = 60 };

    private readonly TemporaryDirectory _directory = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));

    public ReloadingThreatIntelligenceTests()
    {
        _directory.WriteFile(ThreatIntelFiles.BlockedDomains, "domain\n", InitialWriteTime);
    }

    [Fact]
    public void Match_FileChangedBeforeCheckInterval_KeepsOldIndex()
    {
        var intelligence = new ReloadingThreatIntelligence(_directory.Path, Options, _time);
        _directory.WriteFile(ThreatIntelFiles.BlockedDomains, "domain\nvcb-xacminh.com\n", InitialWriteTime.AddMinutes(5));
        _time.Advance(TimeSpan.FromSeconds(30));

        var match = intelligence.Match(Message, MaskedMessage);

        Assert.False(match.IsStrongSignal);
    }

    [Fact]
    public void Match_FileChangedAfterCheckInterval_ReloadsIndex()
    {
        var intelligence = new ReloadingThreatIntelligence(_directory.Path, Options, _time);
        _directory.WriteFile(ThreatIntelFiles.BlockedDomains, "domain\nvcb-xacminh.com\n", InitialWriteTime.AddMinutes(5));
        _time.Advance(TimeSpan.FromSeconds(61));

        var match = intelligence.Match(Message, MaskedMessage);

        Assert.True(match.IsStrongSignal);
        Assert.Equal(1, intelligence.Current.BlockedDomainCount);
    }

    [Fact]
    public void Match_FileUnchangedAfterCheckInterval_KeepsSameIndexInstance()
    {
        var intelligence = new ReloadingThreatIntelligence(_directory.Path, Options, _time);
        var initialIndex = intelligence.Current;
        _time.Advance(TimeSpan.FromSeconds(61));

        intelligence.Match(Message, MaskedMessage);

        Assert.Same(initialIndex, intelligence.Current);
    }

    public void Dispose() => _directory.Dispose();
}
