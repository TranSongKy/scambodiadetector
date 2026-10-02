namespace ScamDetector.Bot.Telegram;

public sealed class TelegramApiException : Exception
{
    public TelegramApiException()
    {
    }

    public TelegramApiException(string message)
        : base(message)
    {
    }

    public TelegramApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TelegramApiException(string message, TimeSpan? retryAfter)
        : base(message)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan? RetryAfter { get; }
}
