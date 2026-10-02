namespace ScamDetector.Bot.Telegram;

public sealed record TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; init; } = string.Empty;

    public Uri ApiBaseUrl { get; init; } = new("https://api.telegram.org");

    public int PollingTimeoutSeconds { get; init; } = 30;

    public int ErrorRetryDelaySeconds { get; init; } = 5;
}
