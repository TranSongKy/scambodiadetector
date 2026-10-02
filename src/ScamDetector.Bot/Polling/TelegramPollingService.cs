using ScamDetector.Bot.Messaging;
using ScamDetector.Bot.Telegram;

namespace ScamDetector.Bot.Polling;

public sealed partial class TelegramPollingService(
    ITelegramClient telegramClient,
    BotReplyBuilder replyBuilder,
    ReportCallbackHandler reportCallbackHandler,
    TelegramOptions options,
    ILogger<TelegramPollingService> logger) : BackgroundService
{
    private long _nextOffset;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogPollingFailed(logger, exception);
                await Task.Delay(RetryDelay(exception), stoppingToken);
            }
        }
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var updates = await telegramClient.GetUpdatesAsync(_nextOffset, cancellationToken);
        foreach (var update in updates)
        {
            _nextOffset = Math.Max(_nextOffset, update.UpdateId + 1);
            try
            {
                await HandleUpdateAsync(update, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogUpdateFailed(logger, update.UpdateId, exception);
            }
        }
    }

    private async Task HandleUpdateAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (update.Message is { } message)
        {
            var reply = await replyBuilder.BuildReplyAsync(message, cancellationToken);
            if (reply is not null)
                await telegramClient.SendReplyAsync(message, reply.Text, reply.OfferReport ? ReportKeyboard.Markup : null, cancellationToken);
        }

        if (update.CallbackQuery is { } callbackQuery)
        {
            var answer = await reportCallbackHandler.HandleAsync(callbackQuery, cancellationToken);
            await telegramClient.AnswerCallbackQueryAsync(callbackQuery.Id, answer, cancellationToken);
        }
    }

    private TimeSpan RetryDelay(Exception exception) =>
        exception is TelegramApiException { RetryAfter: { } retryAfter }
            ? retryAfter
            : TimeSpan.FromSeconds(options.ErrorRetryDelaySeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handling update {UpdateId} failed")]
    private static partial void LogUpdateFailed(ILogger logger, long updateId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram polling failed, retrying")]
    private static partial void LogPollingFailed(ILogger logger, Exception exception);
}
