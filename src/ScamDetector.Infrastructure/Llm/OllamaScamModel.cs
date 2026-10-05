using System.Net.Http.Json;
using System.Text.Json;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.Onnx;

namespace ScamDetector.Infrastructure.Llm;

public sealed class OllamaScamModel(IHttpClientFactory httpClientFactory, LlmModelOptions options) : IScamModel
{
    private const string ChatPath = "api/chat";
    private const string TagsPath = "api/tags";
    private const string DefaultTagSuffix = ":latest";

    public async Task<ModelPrediction> PredictAsync(string maskedText, CancellationToken cancellationToken)
    {
        var request = ScamClassificationPrompt.CreateRequest(options, maskedText);
        LlmVerdict? verdict;
        try
        {
            using var response = await CreateClient().PostAsJsonAsync(ChatPath, request, OllamaJson.Options, cancellationToken);
            response.EnsureSuccessStatusCode();
            var chat = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(OllamaJson.Options, cancellationToken);
            verdict = LlmVerdict.Parse(chat?.Message?.Content);
        }
        catch (Exception exception) when (IsLlmFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }

        return verdict?.ToPrediction() ?? throw Unavailable(null);
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var tags = await CreateClient().GetFromJsonAsync<OllamaTagsResponse>(TagsPath, OllamaJson.Options, cancellationToken);
            var expectedName = WithDefaultTag(options.Model);
            return tags?.Models?.Any(model => WithDefaultTag(model.Name) == expectedName) ?? false;
        }
        catch (Exception exception) when (IsLlmFailure(exception, cancellationToken))
        {
            return false;
        }
    }

    private HttpClient CreateClient() => httpClientFactory.CreateClient(LlmModelOptions.HttpClientName);

    private ScamModelUnavailableException Unavailable(Exception? innerException)
    {
        var message = $"LLM '{options.Model}' at {options.BaseUrl} did not return a valid verdict.";
        return innerException is null
            ? new ScamModelUnavailableException(message)
            : new ScamModelUnavailableException(message, innerException);
    }

    private static bool IsLlmFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or JsonException or NotSupportedException
        || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static string WithDefaultTag(string modelName) =>
        modelName.Contains(':', StringComparison.Ordinal) ? modelName : modelName + DefaultTagSuffix;
}
