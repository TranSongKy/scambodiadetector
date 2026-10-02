namespace ScamDetector.Bot.Telegram;

public sealed record SendMessageRequest(long ChatId, string Text, TelegramReplyParameters ReplyParameters);
