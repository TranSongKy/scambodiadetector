namespace ScamDetector.Core.Urls;

public interface IUrlInspector
{
    Task<IReadOnlyList<UrlFinding>> InspectAsync(string normalizedText, CancellationToken cancellationToken);
}
