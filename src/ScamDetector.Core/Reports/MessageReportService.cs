using ScamDetector.Core.Classification;
using ScamDetector.Core.Common;
using ScamDetector.Core.Text;

namespace ScamDetector.Core.Reports;

public sealed class MessageReportService(IMessageReportRepository repository, TimeProvider timeProvider)
    : IMessageReportService
{
    public async Task<Result<Guid>> SubmitAsync(
        string text,
        MessageLabel reportedLabel,
        ReportChannel channel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Result.Failure<Guid>(ClassificationErrors.EmptyText);

        var normalizedText = TextNormalizer.Normalize(text);
        if (normalizedText.Length > ClassificationLimits.MaxMessageLength)
            return Result.Failure<Guid>(ClassificationErrors.TextTooLong);

        var maskedText = ModelInputMasker.Mask(normalizedText);
        if (maskedText.Length > ClassificationLimits.MaxMessageLength)
            return Result.Failure<Guid>(ClassificationErrors.TextTooLong);

        var createdAt = timeProvider.GetUtcNow();
        var report = new MessageReport(
            Guid.CreateVersion7(createdAt),
            maskedText,
            reportedLabel,
            channel,
            createdAt);
        await repository.AddAsync(report, cancellationToken);

        return report.Id;
    }
}
