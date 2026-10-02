using Microsoft.EntityFrameworkCore;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Reports;
using ScamDetector.Infrastructure.Persistence;

namespace ScamDetector.Infrastructure.Tests.Persistence;

public sealed class EfMessageReportRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 2, 9, 30, 0, TimeSpan.Zero);

    private readonly SqliteDbContextFixture _fixture = new();

    [Fact]
    public async Task AddAsync_Report_PersistsAllFields()
    {
        var report = new MessageReport(
            Guid.CreateVersion7(CreatedAt),
            "Goi <PHONE> ngay",
            MessageLabel.Scam,
            ReportChannel.Telegram,
            CreatedAt);
        await using (var dbContext = _fixture.CreateDbContext())
        {
            await new EfMessageReportRepository(dbContext).AddAsync(report, CancellationToken.None);
        }

        await using var readContext = _fixture.CreateDbContext();
        var stored = await readContext.MessageReports.SingleAsync(CancellationToken.None);

        Assert.Equal(report.Id, stored.Id);
        Assert.Equal("Goi <PHONE> ngay", stored.MaskedText);
        Assert.Equal(MessageLabel.Scam, stored.ReportedLabel);
        Assert.Equal(ReportChannel.Telegram, stored.Channel);
        Assert.Equal(CreatedAt, stored.CreatedAt);
    }

    [Fact]
    public async Task AddAsync_TwoReports_StoresBoth()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new EfMessageReportRepository(dbContext);

        await repository.AddAsync(new MessageReport(Guid.CreateVersion7(), "mot", MessageLabel.Spam, ReportChannel.Api, CreatedAt), CancellationToken.None);
        await repository.AddAsync(new MessageReport(Guid.CreateVersion7(), "hai", MessageLabel.Normal, ReportChannel.Extension, CreatedAt), CancellationToken.None);

        Assert.Equal(2, await dbContext.MessageReports.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnavailableRepository_AddAsync_ThrowsReportsUnavailableException()
    {
        var repository = new UnavailableMessageReportRepository();
        var report = new MessageReport(Guid.CreateVersion7(), "mot", MessageLabel.Spam, ReportChannel.Api, CreatedAt);

        await Assert.ThrowsAsync<ReportsUnavailableException>(() => repository.AddAsync(report, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_DatabaseRejectsWrite_ThrowsReportsUnavailableException()
    {
        var id = Guid.CreateVersion7(CreatedAt);
        await using (var dbContext = _fixture.CreateDbContext())
        {
            await new EfMessageReportRepository(dbContext).AddAsync(
                new MessageReport(id, "mot", MessageLabel.Spam, ReportChannel.Api, CreatedAt),
                CancellationToken.None);
        }

        await using var secondContext = _fixture.CreateDbContext();
        var duplicate = new MessageReport(id, "hai", MessageLabel.Scam, ReportChannel.Api, CreatedAt);

        await Assert.ThrowsAsync<ReportsUnavailableException>(
            () => new EfMessageReportRepository(secondContext).AddAsync(duplicate, CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_SinceAndLimit_ReturnsOldestMatchingReportsFirst()
    {
        await using (var dbContext = _fixture.CreateDbContext())
        {
            var repository = new EfMessageReportRepository(dbContext);
            foreach (var hours in new[] { 3, 1, 2, -1 })
            {
                var createdAt = CreatedAt.AddHours(hours);
                await repository.AddAsync(
                    new MessageReport(Guid.CreateVersion7(createdAt), $"tin {hours}", MessageLabel.Spam, ReportChannel.Api, createdAt),
                    CancellationToken.None);
            }
        }

        await using var readContext = _fixture.CreateDbContext();
        var reports = await new EfMessageReportRepository(readContext).ListAsync(CreatedAt, 2, CancellationToken.None);

        Assert.Equal(["tin 1", "tin 2"], reports.Select(report => report.MaskedText));
    }

    public void Dispose() => _fixture.Dispose();
}
