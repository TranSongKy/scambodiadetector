using ScamDetector.Bot.Messaging;
using ScamDetector.Bot.Telegram;

namespace ScamDetector.Bot.Polling;

public sealed partial class TelegramPollingService(
    ITelegramClient telegramClient,
    BotReplyBuilder replyBuilder,
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
            if (update.Message is not null)
                await HandleMessageSafelyAsync(update.UpdateId, update.Message, cancellationToken);
        }
    }

    private async Task HandleMessageSafelyAsync(long updateId, TelegramMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await replyBuilder.BuildReplyAsync(message, cancellationToken);
            if (reply is not null)
                await telegramClient.SendReplyAsync(message, reply, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogUpdateFailed(logger, updateId, exception);
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
