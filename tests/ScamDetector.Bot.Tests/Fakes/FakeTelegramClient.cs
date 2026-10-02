using ScamDetector.Bot.Telegram;

namespace ScamDetector.Bot.Tests.Fakes;

public sealed class FakeTelegramClient(params IReadOnlyList<TelegramUpdate>[] batches) : ITelegramClient
{
    private int _batchIndex;

    public List<long> RequestedOffsets { get; } = [];

    public List<(TelegramMessage Message, string Text)> SentReplies { get; } = [];

    public HashSet<long> FailingChatIds { get; } = [];

    public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken)
    {
        RequestedOffsets.Add(offset);
        IReadOnlyList<TelegramUpdate> batch = _batchIndex < batches.Length ? batches[_batchIndex] : [];
        _batchIndex++;
        return Task.FromResult(batch);
    }

    public List<TelegramInlineKeyboardMarkup?> SentMarkups { get; } = [];

    public List<(string CallbackQueryId, string Text)> CallbackAnswers { get; } = [];

    public Task AnswerCallbackQueryAsync(string callbackQueryId, string text, CancellationToken cancellationToken)
    {
        CallbackAnswers.Add((callbackQueryId, text));
        return Task.CompletedTask;
    }

    public Task SendReplyAsync(
        TelegramMessage message,
        string text,
        TelegramInlineKeyboardMarkup? replyMarkup,
        CancellationToken cancellationToken)
    {
        if (FailingChatIds.Contains(message.Chat.Id))
            throw new TelegramApiException("Forbidden: bot was blocked by the user");

        SentReplies.Add((message, text));
        SentMarkups.Add(replyMarkup);
        return Task.CompletedTask;
    }
}
