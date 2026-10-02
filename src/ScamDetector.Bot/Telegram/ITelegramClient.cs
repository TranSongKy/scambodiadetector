namespace ScamDetector.Bot.Telegram;

public interface ITelegramClient
{
    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken);

    Task SendReplyAsync(
        TelegramMessage message,
        string text,
        TelegramInlineKeyboardMarkup? replyMarkup,
        CancellationToken cancellationToken);

    Task AnswerCallbackQueryAsync(string callbackQueryId, string text, CancellationToken cancellationToken);
}
