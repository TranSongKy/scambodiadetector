namespace ScamDetector.Bot.Telegram;

public static class TelegramOptionsValidator
{
    private const string BotPathPrefix = "./bot";
    private const string PathSeparator = "/";

    public static TelegramOptions Validate(TelegramOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BotToken))
            throw new InvalidOperationException($"{TelegramOptions.SectionName}:{nameof(TelegramOptions.BotToken)} is required.");
        if (options.PollingTimeoutSeconds <= 0 || options.ErrorRetryDelaySeconds <= 0)
            throw new InvalidOperationException($"{TelegramOptions.SectionName} timeouts must be positive.");
        if (options.ApiBaseUrl.Scheme != Uri.UriSchemeHttps && !options.ApiBaseUrl.IsLoopback)
            throw new InvalidOperationException($"{TelegramOptions.SectionName}:{nameof(TelegramOptions.ApiBaseUrl)} must use https.");
        return options;
    }

    public static Uri BotApiAddress(TelegramOptions options) =>
        new(options.ApiBaseUrl, BotPathPrefix + options.BotToken + PathSeparator);
}
