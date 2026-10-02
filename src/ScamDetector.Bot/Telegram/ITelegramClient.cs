namespace ScamDetector.Bot.Telegram;

public interface ITelegramClient
{
    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken);

    Task SendReplyAsync(TelegramMessage message, string text, CancellationToken cancellationToken);
}
