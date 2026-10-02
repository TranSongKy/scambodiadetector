using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScamDetector.Infrastructure.Persistence;

namespace ScamDetector.Infrastructure.Tests.Persistence;

public sealed class SqliteDbContextFixture : IDisposable
{
    private const string InMemoryConnectionString = "DataSource=:memory:";

    private readonly SqliteConnection _connection = new(InMemoryConnectionString);

    public SqliteDbContextFixture()
    {
        _connection.Open();
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureCreated();
    }

    public ScamDetectorDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ScamDetectorDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
