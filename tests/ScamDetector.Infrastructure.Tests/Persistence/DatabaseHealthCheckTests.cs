using Microsoft.Extensions.Diagnostics.HealthChecks;
using ScamDetector.Infrastructure.HealthChecks;

namespace ScamDetector.Infrastructure.Tests.Persistence;

public sealed class DatabaseHealthCheckTests : IDisposable
{
    private readonly SqliteDbContextFixture _fixture = new();

    [Fact]
    public async Task CheckHealthAsync_ReachableDatabase_ReturnsHealthy()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var healthCheck = new DatabaseHealthCheck(dbContext);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    public void Dispose() => _fixture.Dispose();
}
