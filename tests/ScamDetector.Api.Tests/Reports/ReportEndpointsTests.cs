using System.Net;
using System.Net.Http.Json;
using ScamDetector.Api.Reports;
using ScamDetector.Api.Tests.Fakes;
using ScamDetector.Api.Tests.Support;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Reports;

namespace ScamDetector.Api.Tests.Reports;

public sealed class ReportEndpointsTests
{
    private const string ProblemContentType = "application/problem+json";

    [Fact]
    public async Task Create_ValidReport_StoresMaskedTextAndReturnsCreated()
    {
        var repository = new FakeMessageReportRepository();
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0), reportRepository: repository);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            ApiRoutes.Reports,
            new CreateReportRequest("Goi 0901234567 de nhan qua", "scam", "extension"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateReportResponse>(CancellationToken.None);
        Assert.NotNull(body);
        var stored = Assert.Single(repository.Reports);
        Assert.Equal(body.Id, stored.Id);
        Assert.Equal($"{ApiRoutes.Reports}/{body.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Goi <PHONE> de nhan qua", stored.MaskedText);
        Assert.Equal(MessageLabel.Scam, stored.ReportedLabel);
        Assert.Equal(ReportChannel.Extension, stored.Channel);
    }

    [Fact]
    public async Task Create_ChannelOmitted_DefaultsToApi()
    {
        var repository = new FakeMessageReportRepository();
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0), reportRepository: repository);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(ApiRoutes.Reports, new CreateReportRequest("Mai hop nhom", "normal", null), CancellationToken.None);

        Assert.Equal(ReportChannel.Api, Assert.Single(repository.Reports).Channel);
    }

    [Theory]
    [InlineData("phishing", "api", "report.invalid_label")]
    [InlineData(null, "api", "report.invalid_label")]
    [InlineData("scam", "sms", "report.invalid_channel")]
    public async Task Create_InvalidLabelOrChannel_ReturnsBadRequestProblem(string? label, string channel, string expectedCode)
    {
        var repository = new FakeMessageReportRepository();
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0), reportRepository: repository);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Reports, new CreateReportRequest("xin chao", label, channel), CancellationToken.None);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, expectedCode);
        Assert.Empty(repository.Reports);
    }

    [Fact]
    public async Task Create_EmptyText_ReturnsBadRequestProblem()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0), reportRepository: new FakeMessageReportRepository());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Reports, new CreateReportRequest("  ", "scam", null), CancellationToken.None);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ClassificationErrors.EmptyText.Code);
    }

    [Fact]
    public async Task Create_DatabaseNotConfigured_ReturnsServiceUnavailableProblem()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Reports, new CreateReportRequest("xin chao", "scam", null), CancellationToken.None);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, expectedCode: null);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string? expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(ProblemContentType, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(CancellationToken.None);
        Assert.NotNull(problem);
        Assert.Equal(expectedCode, problem.Code);
    }
}
