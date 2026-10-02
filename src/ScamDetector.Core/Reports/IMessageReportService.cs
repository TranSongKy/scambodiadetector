using ScamDetector.Core.Classification;
using ScamDetector.Core.Common;

namespace ScamDetector.Core.Reports;

public interface IMessageReportService
{
    Task<Result<Guid>> SubmitAsync(
        string text,
        MessageLabel reportedLabel,
        ReportChannel channel,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MessageReport>> ListAsync(DateTimeOffset? since, int? limit, CancellationToken cancellationToken);
}
