namespace ScamDetector.Bot.Telegram;

public sealed record AnswerCallbackQueryRequest(string CallbackQueryId, string Text);
