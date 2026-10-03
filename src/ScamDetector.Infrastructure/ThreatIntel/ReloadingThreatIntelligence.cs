using ScamDetector.Core.ThreatIntel;

namespace ScamDetector.Infrastructure.ThreatIntel;

public sealed class ReloadingThreatIntelligence(string dataDirectory, ThreatIntelOptions options, TimeProvider timeProvider)
    : IThreatIntelligence
{
    private readonly Lock _reloadLock = new();
    private ThreatIntelligenceIndex _index = ThreatIntelLoader.Load(dataDirectory, options.MinimumTemplateCoverage);
    private DateTime _loadedWriteTime = ThreatIntelLoader.LatestWriteTimeUtc(dataDirectory);
    private DateTimeOffset _nextCheck = timeProvider.GetUtcNow().AddSeconds(options.ReloadCheckSeconds);

    public ThreatIntelligenceIndex Current => Volatile.Read(ref _index);

    public ThreatMatch Match(string normalizedText, string maskedText)
    {
        ReloadIfChanged();
        return Current.Match(normalizedText, maskedText);
    }

    private void ReloadIfChanged()
    {
        var now = timeProvider.GetUtcNow();
        if (now < _nextCheck)
            return;

        lock (_reloadLock)
        {
            if (now < _nextCheck)
                return;
            _nextCheck = now.AddSeconds(options.ReloadCheckSeconds);
            var writeTime = ThreatIntelLoader.LatestWriteTimeUtc(dataDirectory);
            if (writeTime == _loadedWriteTime)
                return;
            Volatile.Write(ref _index, ThreatIntelLoader.Load(dataDirectory, options.MinimumTemplateCoverage));
            _loadedWriteTime = writeTime;
        }
    }
}
