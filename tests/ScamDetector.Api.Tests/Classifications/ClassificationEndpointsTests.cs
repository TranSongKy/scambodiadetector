using System.Net;
using System.Net.Http.Json;
using ScamDetector.Api.Classifications;
using ScamDetector.Api.Tests.Fakes;
using ScamDetector.Api.Tests.Support;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Urls;

namespace ScamDetector.Api.Tests.Classifications;

public sealed class ClassificationEndpointsTests
{
    private const string ProblemContentType = "application/problem+json";
    private const string ScamText = "Tai khoan bi khoa, xac minh tai bit.ly/abc ngay";

    [Fact]
    public async Task Classify_ScamAboveThreshold_ReturnsScamWithReasons()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(0.05, 0.05, 0.9));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Classifications, new ClassifyMessageRequest(ScamText), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClassificationResponse>(CancellationToken.None);
        Assert.NotNull(body);
        Assert.Equal(MessageLabelNames.Scam, body.Label);
        Assert.Equal(UrlRisk.CombineWithModel(0.9, UrlRisk.ShortenerWeight), body.Confidence, precision: 10);
        Assert.Contains(ClassificationReasons.ModelPredictedScam, body.Reasons);
        Assert.Contains(UrlReasons.Shortener, body.Reasons);
    }

    [Fact]
    public async Task Classify_NormalMessage_ReturnsLowercaseNormalLabel()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(0.8, 0.1, 0.1));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Classifications, new ClassifyMessageRequest("Mai 7h hop nhom nha"), CancellationToken.None);

        var body = await response.Content.ReadFromJsonAsync<ClassificationResponse>(CancellationToken.None);
        Assert.NotNull(body);
        Assert.Equal(MessageLabelNames.Normal, body.Label);
        Assert.Empty(body.Reasons);
    }

    [Fact]
    public async Task Classify_TextWithUrl_SendsMaskedTextToModel()
    {
        var model = FakeScamModel.Returning(0.8, 0.1, 0.1);
        await using var factory = new ScamDetectorApiFactory(model);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(ApiRoutes.Classifications, new ClassifyMessageRequest(ScamText), CancellationToken.None);

        Assert.Equal("Tai khoan bi khoa, xac minh tai <URL> ngay", model.ReceivedText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Classify_EmptyText_ReturnsBadRequestProblem(string? text)
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Classifications, new ClassifyMessageRequest(text), CancellationToken.None);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ClassificationErrors.EmptyText.Code);
    }

    [Fact]
    public async Task Classify_TextLongerThanLimit_ReturnsBadRequestProblem()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0));
        using var client = factory.CreateClient();
        var text = new string('a', ClassificationLimits.MaxMessageLength + 1);

        var response = await client.PostAsJsonAsync(ApiRoutes.Classifications, new ClassifyMessageRequest(text), CancellationToken.None);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ClassificationErrors.TextTooLong.Code);
    }

    [Fact]
    public async Task Classify_MalformedJson_ReturnsBadRequestProblem()
    {
        await using var factory = new ScamDetectorApiFactory(FakeScamModel.Returning(1, 0, 0));
        using var client = factory.CreateClient();
        using var content = new StringContent("{not json", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync(ApiRoutes.Classifications, content, CancellationToken.None);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, expectedCode: null);
    }

    [Fact]
    public async Task Classify_ModelFilesMissing_ReturnsServiceUnavailableProblem()
    {
        await using var factory = new ScamDetectorApiFactory(settings: FixtureModelSettings.Missing());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Classifications, new ClassifyMessageRequest(ScamText), CancellationToken.None);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, expectedCode: null);
    }

    [Fact]
    public async Task Classify_FixtureOnnxModel_ReturnsClassificationFromRealModel()
    {
        await using var factory = new ScamDetectorApiFactory(settings: FixtureModelSettings.Create());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Classifications, new ClassifyMessageRequest("xin chào"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClassificationResponse>(CancellationToken.None);
        Assert.NotNull(body);
        Assert.Equal(MessageLabelNames.Scam, body.Label);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string? expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(ProblemContentType, response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(CancellationToken.None);
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Equal(expectedCode, problem.Code);
    }
}
