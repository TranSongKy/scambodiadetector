using ScamDetector.Core.Common;

namespace ScamDetector.Core.Links;

public interface ILinkInspectionService
{
    Task<Result<IReadOnlyList<LinkInspection>>> InspectAsync(IReadOnlyList<string> urls, CancellationToken cancellationToken);
}
