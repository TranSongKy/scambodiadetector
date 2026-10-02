using ScamDetector.Core.Reports;

namespace ScamDetector.Core.Tests.Fakes;

public sealed class FakeMessageReportRepository : IMessageReportRepository
{
    private readonly List<MessageReport> _addedReports = [];

    public IReadOnlyList<MessageReport> AddedReports => _addedReports;

    public int CallCount { get; private set; }

    public CancellationToken ReceivedToken { get; private set; }

    public Exception? ExceptionToThrow { get; init; }

    public Task AddAsync(MessageReport report, CancellationToken cancellationToken)
    {
        CallCount++;
        ReceivedToken = cancellationToken;
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;

        _addedReports.Add(report);
        return Task.CompletedTask;
    }

    public (DateTimeOffset? Since, int Limit)? ReceivedListQuery { get; private set; }

    public Task<IReadOnlyList<MessageReport>> ListAsync(DateTimeOffset? since, int limit, CancellationToken cancellationToken)
    {
        ReceivedListQuery = (since, limit);
        return Task.FromResult<IReadOnlyList<MessageReport>>(_addedReports);
    }
}
