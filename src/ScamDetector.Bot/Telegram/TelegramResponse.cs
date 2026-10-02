namespace ScamDetector.Bot.Telegram;

public sealed record TelegramResponse<TResult>(bool Ok, TResult? Result, string? Description);
