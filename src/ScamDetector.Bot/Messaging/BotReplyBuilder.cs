using ScamDetector.Bot.Telegram;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.Onnx;

namespace ScamDetector.Bot.Messaging;

public sealed class BotReplyBuilder(IMessageClassifier classifier)
{
    private const char BotMentionSeparator = '@';

    public async Task<BotReply?> BuildReplyAsync(TelegramMessage message, CancellationToken cancellationToken)
    {
        var content = message.Content;
        if (content is null)
            return null;
        if (IsCommand(content, BotCommands.Start) || IsCommand(content, BotCommands.Help))
            return new BotReply(BotReplies.Welcome, OfferReport: false);

        try
        {
            var result = await classifier.ClassifyAsync(content, cancellationToken);
            return result.IsSuccess
                ? new BotReply(ReplyFormatter.FormatClassification(result.Value), OfferReport: true)
                : new BotReply(ReplyFormatter.FormatError(result.Error!), OfferReport: false);
        }
        catch (ScamModelUnavailableException)
        {
            return new BotReply(BotReplies.ModelUnavailable, OfferReport: false);
        }
    }

    private static bool IsCommand(string content, string command)
    {
        var firstWord = content.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return firstWord.Split(BotMentionSeparator, 2)[0].Equals(command, StringComparison.OrdinalIgnoreCase);
    }
}
