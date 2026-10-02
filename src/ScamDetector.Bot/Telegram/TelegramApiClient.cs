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
    private const string AllowedUpdates = "[\"message\"]";

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken)
    {
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"{GetUpdatesMethod}?offset={offset}&timeout={options.PollingTimeoutSeconds}&allowed_updates={Uri.EscapeDataString(AllowedUpdates)}");
        var response = await httpClient.GetFromJsonAsync<TelegramResponse<List<TelegramUpdate>>>(
            new Uri(query, UriKind.Relative),
            TelegramJson.Options,
            cancellationToken);
        return EnsureOk(response) ?? [];
    }

    public async Task SendReplyAsync(TelegramMessage message, string text, CancellationToken cancellationToken)
    {
        var request = new SendMessageRequest(message.Chat.Id, text, new TelegramReplyParameters(message.MessageId));
        using var content = new StringContent(
            JsonSerializer.Serialize(request, TelegramJson.Options),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);
        using var httpResponse = await httpClient.PostAsync(
            new Uri(SendMessageMethod, UriKind.Relative),
            content,
            cancellationToken);
        var response = await httpResponse.Content.ReadFromJsonAsync<TelegramResponse<TelegramMessage>>(
            TelegramJson.Options,
            cancellationToken);
        EnsureOk(response);
    }

    private static TResult? EnsureOk<TResult>(TelegramResponse<TResult>? response) =>
        response is { Ok: true }
            ? response.Result
            : throw new TelegramApiException($"Telegram API error: {response?.Description ?? "empty response"}");
}
