using ScamDetector.Core.Reports;

namespace ScamDetector.Infrastructure.Persistence;

public sealed class UnavailableMessageReportRepository : IMessageReportRepository
{
    private const string NotConfiguredMessage = "Database connection string 'ScamDetector' is not configured.";

    public Task AddAsync(MessageReport report, CancellationToken cancellationToken) =>
        throw new ReportsUnavailableException(NotConfiguredMessage);

    public Task<IReadOnlyList<MessageReport>> ListAsync(DateTimeOffset? since, int limit, CancellationToken cancellationToken) =>
        throw new ReportsUnavailableException(NotConfiguredMessage);
}
