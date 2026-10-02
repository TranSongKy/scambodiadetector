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
}
