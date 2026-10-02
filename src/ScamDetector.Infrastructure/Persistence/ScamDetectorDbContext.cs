using Microsoft.EntityFrameworkCore;
using ScamDetector.Core.Reports;

namespace ScamDetector.Infrastructure.Persistence;

public sealed class ScamDetectorDbContext(DbContextOptions<ScamDetectorDbContext> options) : DbContext(options)
{
    public DbSet<MessageReport> MessageReports => Set<MessageReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScamDetectorDbContext).Assembly);
}
