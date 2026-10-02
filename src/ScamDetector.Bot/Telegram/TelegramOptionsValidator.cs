namespace ScamDetector.Bot.Telegram;

public static class TelegramOptionsValidator
{
    public static TelegramOptions Validate(TelegramOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BotToken))
            throw new InvalidOperationException($"{TelegramOptions.SectionName}:{nameof(TelegramOptions.BotToken)} is required.");
        if (options.PollingTimeoutSeconds <= 0 || options.ErrorRetryDelaySeconds <= 0)
            throw new InvalidOperationException($"{TelegramOptions.SectionName} timeouts must be positive.");
        return options;
    }

    public static Uri BotApiAddress(TelegramOptions options) =>
        new(options.ApiBaseUrl, $"bot{options.BotToken}/");
}
