using System.Net;
using System.Net.Http.Json;
using ScamDetector.Api.Reports;
using ScamDetector.Api.Tests.Fakes;
using ScamDetector.Api.Tests.Support;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Reports;

namespace ScamDetector.Api.Tests.Reports;

public sealed class ReportListingEndpointTests
{
    private const string AdminKey = "test-admin-key";

    private static readonly Dictionary<string, string?> AdminSettings = new() { ["ReportAdmin:ApiKey"] = AdminKey };

    private static FakeMessageReportRepository RepositoryWithOneReport()
    {
        var repository = new FakeMessageReportRepository();
        var createdAt = new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
        repository.Reports.Add(new MessageReport(Guid.CreateVersion7(createdAt), "Goi <PHONE>", MessageLabel.Scam, ReportChannel.Telegram, createdAt));
        return repository;
    }

    [Fact]
    public async Task List_ValidApiKey_ReturnsMaskedReports()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0), AdminSettings, RepositoryWithOneReport());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ReportAdminOptions.ApiKeyHeaderName, AdminKey);

        var reports = await client.GetFromJsonAsync<List<ReportResponse>>(ApiRoutes.Reports, CancellationToken.None);

        var report = Assert.Single(reports!);
        Assert.Equal("Goi <PHONE>", report.Text);
        Assert.Equal(MessageLabelNames.Scam, report.Label);
        Assert.Equal(ReportChannelNames.Telegram, report.Channel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task List_MissingOrWrongApiKey_ReturnsUnauthorized(string? apiKey)
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0), AdminSettings, RepositoryWithOneReport());
        using var client = factory.CreateClient();
        if (apiKey is not null)
            client.DefaultRequestHeaders.Add(ReportAdminOptions.ApiKeyHeaderName, apiKey);

        var response = await client.GetAsync(ApiRoutes.Reports, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_ApiKeyNotConfigured_ReturnsNotFound()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0), reportRepository: RepositoryWithOneReport());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ReportAdminOptions.ApiKeyHeaderName, string.Empty);

        var response = await client.GetAsync(ApiRoutes.Reports, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_SinceAfterAllReports_ReturnsEmptyList()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0), AdminSettings, RepositoryWithOneReport());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ReportAdminOptions.ApiKeyHeaderName, AdminKey);

        var reports = await client.GetFromJsonAsync<List<ReportResponse>>(
            $"{ApiRoutes.Reports}?since=2026-10-03T00:00:00Z&limit=10",
            CancellationToken.None);

        Assert.Empty(reports!);
    }
}
