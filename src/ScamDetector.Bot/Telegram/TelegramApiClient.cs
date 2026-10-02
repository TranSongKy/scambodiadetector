using System.Globalization;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace ScamDetector.Bot.Telegram;

public sealed class TelegramApiClient(HttpClient httpClient, TelegramOptions options) : ITelegramClient
{
    private const string GetUpdatesMethod = "getUpdates";
    private const string SendMessageMethod = "sendMessage";
    private const string AnswerCallbackQueryMethod = "answerCallbackQuery";
    private const string AllowedUpdates = "[\"message\",\"callback_query\"]";
    private const string EmptyResponseDescription = "empty response";

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"{GetUpdatesMethod}?offset={offset}&timeout={options.PollingTimeoutSeconds}&allowed_updates={Uri.EscapeDataString(AllowedUpdates)}");
        using var httpResponse = await httpClient.GetAsync(new Uri(query, UriKind.Relative), cancellationToken);
        var updates = await ReadResultAsync<List<TelegramUpdate>>(httpResponse, cancellationToken);
        return updates ?? [];
    }

    public async Task SendReplyAsync(
        TelegramMessage message,
        string text,
        TelegramInlineKeyboardMarkup? replyMarkup,
        CancellationToken cancellationToken)
    {
        var request = new SendMessageRequest(message.Chat.Id, text, new TelegramReplyParameters(message.MessageId), replyMarkup);
        await PostAsync<SendMessageRequest, TelegramMessage>(SendMessageMethod, request, cancellationToken);
    }

    public async Task AnswerCallbackQueryAsync(string callbackQueryId, string text, CancellationToken cancellationToken)
    {
        var request = new AnswerCallbackQueryRequest(callbackQueryId, text);
        await PostAsync<AnswerCallbackQueryRequest, bool>(AnswerCallbackQueryMethod, request, cancellationToken);
    }

    private async Task PostAsync<TRequest, TResult>(string method, TRequest request, CancellationToken cancellationToken)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(request, TelegramJson.Options),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);
        using var httpResponse = await httpClient.PostAsync(new Uri(method, UriKind.Relative), content, cancellationToken);
        await ReadResultAsync<TResult>(httpResponse, cancellationToken);
    }

    private static async Task<TResult?> ReadResultAsync<TResult>(HttpResponseMessage httpResponse, CancellationToken cancellationToken)
    {
        TelegramResponse<TResult>? response;
        try
        {
            response = await httpResponse.Content.ReadFromJsonAsync<TelegramResponse<TResult>>(TelegramJson.Options, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new TelegramApiException($"Telegram API returned HTTP {(int)httpResponse.StatusCode} with an invalid body.", exception);
        }

        if (response is { Ok: true } && httpResponse.IsSuccessStatusCode)
            return response.Result;

        var retryAfter = response?.Parameters?.RetryAfter is { } seconds ? TimeSpan.FromSeconds(seconds) : (TimeSpan?)null;
        throw new TelegramApiException(
            $"Telegram API error {(int)httpResponse.StatusCode}: {response?.Description ?? EmptyResponseDescription}",
            retryAfter);
    }
}
