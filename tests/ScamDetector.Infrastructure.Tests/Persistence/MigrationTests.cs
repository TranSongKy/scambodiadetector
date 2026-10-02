using Microsoft.EntityFrameworkCore;
using ScamDetector.Infrastructure.Persistence;

namespace ScamDetector.Infrastructure.Tests.Persistence;

public sealed class MigrationTests
{
    [Fact]
    public void Migrations_CurrentModel_HasNoPendingChanges()
    {
        using var dbContext = new DesignTimeDbContextFactory().CreateDbContext([]);

        var hasPendingChanges = dbContext.Database.HasPendingModelChanges();

        Assert.False(hasPendingChanges, "Run: dotnet ef migrations add <Name> --project src/ScamDetector.Infrastructure --output-dir Persistence/Migrations");
    }

    [Fact]
    public void Migrations_Assembly_ContainsInitialCreate()
    {
        using var dbContext = new DesignTimeDbContextFactory().CreateDbContext([]);

        var migrations = dbContext.Database.GetMigrations();

        Assert.Contains(migrations, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }
}
