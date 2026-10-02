using ScamDetector.Core.Reports;

namespace ScamDetector.Infrastructure.Persistence;

public sealed class EfMessageReportRepository(ScamDetectorDbContext dbContext) : IMessageReportRepository
{
    public async Task AddAsync(MessageReport report, CancellationToken cancellationToken)
    {
        dbContext.MessageReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
