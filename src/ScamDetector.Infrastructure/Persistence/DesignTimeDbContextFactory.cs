using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ScamDetector.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ScamDetectorDbContext>
{
    private const string DesignTimeConnectionString = "Server=localhost;Database=ScamDetector;Trusted_Connection=True;TrustServerCertificate=True";

    public ScamDetectorDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ScamDetectorDbContext>().UseSqlServer(DesignTimeConnectionString).Options);
}
