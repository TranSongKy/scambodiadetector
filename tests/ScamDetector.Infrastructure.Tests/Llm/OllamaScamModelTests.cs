using System.Net;
using System.Text.Json;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.Llm;
using ScamDetector.Infrastructure.Onnx;
using ScamDetector.Infrastructure.Tests.Support;

namespace ScamDetector.Infrastructure.Tests.Llm;

public sealed class OllamaScamModelTests
{
    private static readonly Uri BaseAddress = new("http://ollama.test/");
    private static readonly LlmModelOptions Options = new() { BaseUrl = BaseAddress.ToString(), Model = "qwen2.5:3b" };

    [Fact]
    public async Task PredictAsync_ScamVerdict_ReturnsPredictionFromVerdict()
    {
        var handler = StubHttpMessageHandler.Json(ChatResponse("""{"label":"scam","confidence":0.9}"""));

        var prediction = await CreateModel(handler).PredictAsync("Nhấn <URL>", CancellationToken.None);

        Assert.Equal(0.9, prediction.ProbabilityOf(MessageLabel.Scam), precision: 10);
    }

    [Fact]
    public async Task PredictAsync_Always_PostsMaskedTextToChatEndpointOfNamedClient()
    {
        var handler = StubHttpMessageHandler.Json(ChatResponse("""{"label":"normal","confidence":0.9}"""));
        var factory = new StubHttpClientFactory(handler, BaseAddress);

        await new OllamaScamModel(factory, Options).PredictAsync("Mai họp lúc 7h", CancellationToken.None);

        Assert.Equal(LlmModelOptions.HttpClientName, factory.RequestedName);
        Assert.Equal(HttpMethod.Post, handler.CapturedRequest!.Method);
        Assert.Equal(new Uri(BaseAddress, "api/chat"), handler.CapturedRequest.RequestUri);
        using var body = JsonDocument.Parse(handler.CapturedBody!);
        Assert.Equal("qwen2.5:3b", body.RootElement.GetProperty("model").GetString());
        Assert.Contains("Mai họp lúc 7h", body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Theory]
    [InlineData("""{"label":"phishing","confidence":0.9}""")]
    [InlineData("")]
    [InlineData("không phải JSON")]
    public async Task PredictAsync_InvalidVerdict_ThrowsScamModelUnavailableException(string content)
    {
        var handler = StubHttpMessageHandler.Json(ChatResponse(content));

        await Assert.ThrowsAsync<ScamModelUnavailableException>(
            () => CreateModel(handler).PredictAsync("x", CancellationToken.None));
    }

    [Fact]
    public async Task PredictAsync_ModelNotPulled_ThrowsScamModelUnavailableException()
    {
        var handler = StubHttpMessageHandler.Json("""{"error":"model not found"}""", HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<ScamModelUnavailableException>(
            () => CreateModel(handler).PredictAsync("x", CancellationToken.None));
    }

    [Fact]
    public async Task PredictAsync_OllamaUnreachable_ThrowsScamModelUnavailableException()
    {
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("connection refused"));

        var exception = await Assert.ThrowsAsync<ScamModelUnavailableException>(
            () => CreateModel(handler).PredictAsync("x", CancellationToken.None));
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task PredictAsync_RequestTimesOut_ThrowsScamModelUnavailableException()
    {
        var handler = StubHttpMessageHandler.Throwing(new TaskCanceledException("timeout"));

        await Assert.ThrowsAsync<ScamModelUnavailableException>(
            () => CreateModel(handler).PredictAsync("x", CancellationToken.None));
    }

    [Fact]
    public async Task PredictAsync_NonJsonContentType_ThrowsScamModelUnavailableException()
    {
        var handler = StubHttpMessageHandler.WithContentType("<html></html>", "text/html");

        await Assert.ThrowsAsync<ScamModelUnavailableException>(
            () => CreateModel(handler).PredictAsync("x", CancellationToken.None));
    }

    [Fact]
    public async Task IsReadyAsync_NonJsonContentType_ReturnsFalse()
    {
        var handler = StubHttpMessageHandler.WithContentType("<html></html>", "text/html");

        Assert.False(await CreateModel(handler).IsReadyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PredictAsync_CallerCancels_PropagatesCancellation()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var handler = StubHttpMessageHandler.Json(ChatResponse("""{"label":"scam","confidence":0.9}"""));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateModel(handler).PredictAsync("x", source.Token));
    }

    [Theory]
    [InlineData("qwen2.5:3b", "qwen2.5:3b", true)]
    [InlineData("qwen2.5", "qwen2.5:latest", true)]
    [InlineData("qwen2.5:3b", "llama3.2:3b", false)]
    public async Task IsReadyAsync_TagsListed_ReturnsWhetherConfiguredModelIsPulled(
        string configuredModel,
        string pulledModel,
        bool expected)
    {
        var handler = StubHttpMessageHandler.Json($$"""{"models":[{"name":"{{pulledModel}}"}]}""");
        var model = new OllamaScamModel(new StubHttpClientFactory(handler, BaseAddress), Options with { Model = configuredModel });

        var isReady = await model.IsReadyAsync(CancellationToken.None);

        Assert.Equal(expected, isReady);
        Assert.Equal(new Uri(BaseAddress, "api/tags"), handler.CapturedRequest!.RequestUri);
    }

    [Fact]
    public async Task IsReadyAsync_OllamaUnreachable_ReturnsFalse()
    {
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("connection refused"));

        Assert.False(await CreateModel(handler).IsReadyAsync(CancellationToken.None));
    }

    [Fact]
    public void IsAvailable_Configured_ReturnsTrue()
    {
        var model = CreateModel(StubHttpMessageHandler.Json("{}"));

        Assert.True(((IScamModel)model).IsAvailable);
    }

    private static OllamaScamModel CreateModel(StubHttpMessageHandler handler) =>
        new(new StubHttpClientFactory(handler, BaseAddress), Options);

    private static string ChatResponse(string content) =>
        JsonSerializer.Serialize(new { message = new { role = "assistant", content }, done = true });
}
