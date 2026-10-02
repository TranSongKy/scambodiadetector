using ScamDetector.Core.Reports;

namespace ScamDetector.Api.Tests.Fakes;

public sealed class FakeMessageReportRepository : IMessageReportRepository
{
    public List<MessageReport> Reports { get; } = [];

    public Task AddAsync(MessageReport report, CancellationToken cancellationToken)
    {
        Reports.Add(report);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MessageReport>> ListAsync(DateTimeOffset? since, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MessageReport>>(
            Reports.Where(report => since is null || report.CreatedAt >= since).Take(limit).ToList());
}
