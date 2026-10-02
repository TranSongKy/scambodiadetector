namespace ScamDetector.Bot.Telegram;

public sealed record TelegramMessage(long MessageId, TelegramChat Chat, string? Text, string? Caption)
{
    public string? Content => Text ?? Caption;
}
