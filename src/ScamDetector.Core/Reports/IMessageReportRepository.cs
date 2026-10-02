namespace ScamDetector.Core.Reports;

public interface IMessageReportRepository
{
    Task AddAsync(MessageReport report, CancellationToken cancellationToken);

    Task<IReadOnlyList<MessageReport>> ListAsync(DateTimeOffset? since, int limit, CancellationToken cancellationToken);
}
