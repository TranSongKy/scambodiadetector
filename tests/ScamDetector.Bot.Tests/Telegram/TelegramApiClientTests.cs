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
            $"{BaseAddress}getUpdates?offset=11&timeout=25&allowed_updates=%5B%22message%22%5D",
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

        await client.SendReplyAsync(message, "Chào bạn", CancellationToken.None);

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
            () => client.SendReplyAsync(message, "text", CancellationToken.None));
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

        await Assert.ThrowsAsync<TelegramApiException>(() => client.SendReplyAsync(message, "reply", CancellationToken.None));
    }
}
