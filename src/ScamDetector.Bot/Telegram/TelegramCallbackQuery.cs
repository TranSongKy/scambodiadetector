namespace ScamDetector.Bot.Telegram;

public sealed record TelegramCallbackQuery(string Id, TelegramMessage? Message, string? Data);
