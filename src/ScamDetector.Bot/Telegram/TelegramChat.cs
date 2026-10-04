namespace ScamDetector.Bot.Telegram;

public sealed record TelegramChat(long Id, string? Type = null)
{
    private const string PrivateChatType = "private";

    public bool IsPrivate => string.Equals(Type, PrivateChatType, StringComparison.Ordinal);
}
