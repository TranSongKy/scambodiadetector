namespace ScamDetector.Core.Reports;

public interface IMessageReportRepository
{
    Task AddAsync(MessageReport report, CancellationToken cancellationToken);
}
