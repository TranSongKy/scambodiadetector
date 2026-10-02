namespace ScamDetector.Bot.Telegram;

public sealed record TelegramInlineKeyboardMarkup(IReadOnlyList<IReadOnlyList<TelegramInlineKeyboardButton>> InlineKeyboard);
