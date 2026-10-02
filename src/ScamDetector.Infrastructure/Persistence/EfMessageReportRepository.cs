using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ScamDetector.Core.Reports;

namespace ScamDetector.Infrastructure.Persistence;

public sealed class EfMessageReportRepository(ScamDetectorDbContext dbContext) : IMessageReportRepository
{
    private const string SaveFailedMessage = "Saving the report to the database failed.";

    public async Task AddAsync(MessageReport report, CancellationToken cancellationToken)
    {
        dbContext.MessageReports.Add(report);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            throw new ReportsUnavailableException(SaveFailedMessage, exception);
        }
    }
}
