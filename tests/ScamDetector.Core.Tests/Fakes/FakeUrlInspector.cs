using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Fakes;

public sealed class FakeUrlInspector(IReadOnlyList<UrlFinding> findings) : IUrlInspector
{
    public int CallCount { get; private set; }

    public string? ReceivedText { get; private set; }

    public CancellationToken ReceivedToken { get; private set; }

    public Task<IReadOnlyList<UrlFinding>> InspectAsync(string normalizedText, CancellationToken cancellationToken)
    {
        CallCount++;
        ReceivedText = normalizedText;
        ReceivedToken = cancellationToken;
        return Task.FromResult(findings);
    }
}
