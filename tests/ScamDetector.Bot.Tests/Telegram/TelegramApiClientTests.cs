using ScamDetector.Bot.Messaging;
using System.Net;
using System.Text.Json;
using ScamDetector.Bot.Telegram;
using ScamDetector.Bot.Tests.Fakes;

namespace ScamDetector.Bot.Tests.Telegram;

public sealed class TelegramApiClientTests
{
    private const string BaseAddress = "https://api.telegram.org/botfake-token/";
    private const string UpdatesJson = """{"ok":true,"result":[{"update_id":5,"message":{"message_id":7,"chat":{"id":42},"text":"hi"}}]}""";

    private static TelegramApiClient CreateClient(FakeHttpMessageHandler handler, int pollingTimeoutSeconds = 30)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        return new TelegramApiClient(httpClient, new TelegramOptions { PollingTimeoutSeconds = pollingTimeoutSeconds });
    }

    [Fact]
    public async Task GetUpdatesAsync_ValidResponse_ParsesSnakeCaseUpdates()
    {
        using var handler = new FakeHttpMessageHandler(UpdatesJson);
        var client = CreateClient(handler);

        var updates = await client.GetUpdatesAsync(0, CancellationToken.None);

        var update = Assert.Single(updates);
        Assert.Equal(5, update.UpdateId);
        Assert.Equal(7, update.Message!.MessageId);
        Assert.Equal(42, update.Message.Chat.Id);
        Assert.Equal("hi", update.Message.Text);
        Assert.Null(update.Message.Caption);
    }

    [Fact]
    public async Task GetUpdatesAsync_Called_SendsGetWithOffsetTimeoutAndAllowedUpdates()
    {
        using var handler = new FakeHttpMessageHandler(UpdatesJson);
        var client = CreateClient(handler, pollingTimeoutSeconds: 25);

        await client.GetUpdatesAsync(11, CancellationToken.None);

        var request = handler.CapturedRequest!;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            $"{BaseAddress}getUpdates?offset=11&timeout=25&allowed_updates=%5B%22message%22%2C%22callback_query%22%5D",
            request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetUpdatesAsync_EmptyResult_ReturnsEmptyList()
    {
        using var handler = new FakeHttpMessageHandler("""{"ok":true,"result":[]}""");
        var client = CreateClient(handler);

        var updates = await client.GetUpdatesAsync(0, CancellationToken.None);

        Assert.Empty(updates);
    }

    [Fact]
    public async Task GetUpdatesAsync_OkFalse_ThrowsTelegramApiExceptionWithDescription()
    {
        using var handler = new FakeHttpMessageHandler("""{"ok":false,"description":"Unauthorized"}""");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<TelegramApiException>(
            () => client.GetUpdatesAsync(0, CancellationToken.None));

        Assert.Contains("Unauthorized", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendReplyAsync_ValidMessage_PostsJsonWithChatTextAndReplyParameters()
    {
        using var handler = new FakeHttpMessageHandler("""{"ok":true,"result":{"message_id":8,"chat":{"id":42}}}""");
        var client = CreateClient(handler);
        var message = new TelegramMessage(7, new TelegramChat(42), "hi", null);

        await client.SendReplyAsync(message, "Chào bạn", null, CancellationToken.None);

        var request = handler.CapturedRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"{BaseAddress}sendMessage", request.RequestUri!.AbsoluteUri);
        using var body = JsonDocument.Parse(handler.CapturedBody!);
        Assert.Equal(42, body.RootElement.GetProperty("chat_id").GetInt64());
        Assert.Equal("Chào bạn", body.RootElement.GetProperty("text").GetString());
        Assert.Equal(7, body.RootElement.GetProperty("reply_parameters").GetProperty("message_id").GetInt64());
    }

    [Fact]
    public async Task SendReplyAsync_OkFalse_ThrowsTelegramApiException()
    {
        using var handler = new FakeHttpMessageHandler("""{"ok":false,"description":"Bad Request"}""");
        var client = CreateClient(handler);
        var message = new TelegramMessage(7, new TelegramChat(42), "hi", null);

        await Assert.ThrowsAsync<TelegramApiException>(
            () => client.SendReplyAsync(message, "text", null, CancellationToken.None));
    }

    [Fact]
    public async Task GetUpdatesAsync_TooManyRequests_ThrowsWithRetryAfter()
    {
        using var handler = new FakeHttpMessageHandler(
            """{"ok":false,"error_code":429,"description":"Too Many Requests","parameters":{"retry_after":7}}""",
            HttpStatusCode.TooManyRequests);
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<TelegramApiException>(() => client.GetUpdatesAsync(0, CancellationToken.None));

        Assert.Equal(TimeSpan.FromSeconds(7), exception.RetryAfter);
    }

    [Fact]
    public async Task GetUpdatesAsync_HtmlErrorBody_ThrowsTelegramApiException()
    {
        using var handler = new FakeHttpMessageHandler("<html>502 Bad Gateway</html>", HttpStatusCode.BadGateway);
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<TelegramApiException>(() => client.GetUpdatesAsync(0, CancellationToken.None));

        Assert.Null(exception.RetryAfter);
    }

    [Fact]
    public async Task SendReplyAsync_ServerErrorWithOkBody_ThrowsTelegramApiException()
    {
        using var handler = new FakeHttpMessageHandler("""{"ok":true,"result":null}""", HttpStatusCode.InternalServerError);
        var client = CreateClient(handler);
        var message = new TelegramMessage(1, new TelegramChat(42), "hi", null);

        await Assert.ThrowsAsync<TelegramApiException>(() => client.SendReplyAsync(message, "reply", null, CancellationToken.None));
    }

    [Fact]
    public async Task SendReplyAsync_WithKeyboard_SerializesInlineKeyboardAndOmitsNulls()
    {
        using var handler = new FakeHttpMessageHandler("""{"ok":true,"result":{"message_id":9,"chat":{"id":42}}}""");
        var client = CreateClient(handler);
        var message = new TelegramMessage(5, new TelegramChat(42), "hi", null);

        await client.SendReplyAsync(message, "reply", ReportKeyboard.Markup, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.CapturedBody!);
        var buttons = body.RootElement.GetProperty("reply_markup").GetProperty("inline_keyboard")[0];
        Assert.Equal(3, buttons.GetArrayLength());
        Assert.Equal("report:scam", buttons[0].GetProperty("callback_data").GetString());
    }

    [Fact]
    public async Task SendReplyAsync_WithoutKeyboard_OmitsReplyMarkup()
    {
        using var handler = new FakeHttpMessageHandler("""{"ok":true,"result":{"message_id":9,"chat":{"id":42}}}""");
        var client = CreateClient(handler);
        var message = new TelegramMessage(5, new TelegramChat(42), "hi", null);

        await client.SendReplyAsync(message, "reply", null, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.CapturedBody!);
        Assert.False(body.RootElement.TryGetProperty("reply_markup", out _));
    }

    [Fact]
    public async Task AnswerCallbackQueryAsync_Called_PostsCallbackIdAndText()
    {
        using var handler = new FakeHttpMessageHandler("""{"ok":true,"result":true}""");
        var client = CreateClient(handler);

        await client.AnswerCallbackQueryAsync("cb-1", "Cảm ơn", CancellationToken.None);

        Assert.EndsWith("answerCallbackQuery", handler.CapturedRequest!.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(handler.CapturedBody!);
        Assert.Equal("cb-1", body.RootElement.GetProperty("callback_query_id").GetString());
        Assert.Equal("Cảm ơn", body.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task GetUpdatesAsync_CallbackQueryUpdate_ParsesReplyToMessage()
    {
        using var handler = new FakeHttpMessageHandler(
            """{"ok":true,"result":[{"update_id":8,"callback_query":{"id":"cb-1","data":"report:spam","message":{"message_id":3,"chat":{"id":42},"text":"bot reply","reply_to_message":{"message_id":2,"chat":{"id":42},"text":"tin goc"}}}}]}""");
        var client = CreateClient(handler);

        var update = Assert.Single(await client.GetUpdatesAsync(0, CancellationToken.None));

        Assert.Null(update.Message);
        Assert.Equal("report:spam", update.CallbackQuery!.Data);
        Assert.Equal("tin goc", update.CallbackQuery.Message!.ReplyToMessage!.Content);
    }
}
