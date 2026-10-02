using Microsoft.Extensions.Diagnostics.HealthChecks;
using ScamDetector.Infrastructure.Persistence;

namespace ScamDetector.Infrastructure.HealthChecks;

public sealed class DatabaseHealthCheck(ScamDetectorDbContext dbContext) : IHealthCheck
{
    private const string UnreachableDescription = "Database is unreachable.";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken) =>
        await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(UnreachableDescription);
}
