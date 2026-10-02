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
            catch (Exception exception) when (exception is HttpRequestException or TelegramApiException or TaskCanceledException
                                              && !stoppingToken.IsCancellationRequested)
            {
                LogPollingFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(options.ErrorRetryDelaySeconds), stoppingToken);
            }
        }
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var updates = await telegramClient.GetUpdatesAsync(_nextOffset, cancellationToken);
        foreach (var update in updates)
        {
            _nextOffset = Math.Max(_nextOffset, update.UpdateId + 1);
            if (update.Message is null)
                continue;

            var reply = await replyBuilder.BuildReplyAsync(update.Message, cancellationToken);
            if (reply is not null)
                await SendReplySafelyAsync(update, reply, cancellationToken);
        }
    }

    private async Task SendReplySafelyAsync(TelegramUpdate update, string reply, CancellationToken cancellationToken)
    {
        try
        {
            await telegramClient.SendReplyAsync(update.Message!, reply, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TelegramApiException)
        {
            LogReplyFailed(logger, update.UpdateId, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending reply for update {UpdateId} failed")]
    private static partial void LogReplyFailed(ILogger logger, long updateId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram polling failed, retrying")]
    private static partial void LogPollingFailed(ILogger logger, Exception exception);
}
