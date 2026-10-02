using ScamDetector.Bot.Telegram;
using ScamDetector.Core.Classification;

namespace ScamDetector.Bot.Messaging;

public static class ReportKeyboard
{
    public static readonly TelegramInlineKeyboardMarkup Markup = new(
    [
        [
            new TelegramInlineKeyboardButton(BotReplies.ReportScamButton, ReportCallbackData.For(MessageLabel.Scam)),
            new TelegramInlineKeyboardButton(BotReplies.ReportSpamButton, ReportCallbackData.For(MessageLabel.Spam)),
            new TelegramInlineKeyboardButton(BotReplies.ReportNormalButton, ReportCallbackData.For(MessageLabel.Normal)),
        ],
    ]);
}
