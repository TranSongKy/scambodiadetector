using ScamDetector.Bot.Telegram;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.Onnx;

namespace ScamDetector.Bot.Messaging;

public sealed class BotReplyBuilder(IMessageClassifier classifier)
{
    private const char BotMentionSeparator = '@';

    public async Task<string?> BuildReplyAsync(TelegramMessage message, CancellationToken cancellationToken)
    {
        var content = message.Content;
        if (content is null)
            return null;
        if (IsCommand(content, BotCommands.Start) || IsCommand(content, BotCommands.Help))
            return BotReplies.Welcome;

        try
        {
            var result = await classifier.ClassifyAsync(content, cancellationToken);
            return result.IsSuccess
                ? ReplyFormatter.FormatClassification(result.Value)
                : ReplyFormatter.FormatError(result.Error!);
        }
        catch (ScamModelUnavailableException)
        {
            return BotReplies.ModelUnavailable;
        }
    }


    private static bool IsCommand(string content, string command)
    {
        var firstWord = content.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return firstWord.Split(BotMentionSeparator, 2)[0].Equals(command, StringComparison.OrdinalIgnoreCase);
    }
}
