namespace ScamDetector.Bot.Telegram;

public sealed record TelegramUpdate(long UpdateId, TelegramMessage? Message);
