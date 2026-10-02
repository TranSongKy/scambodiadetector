using ScamDetector.Bot.Telegram;
using ScamDetector.Core.Reports;

namespace ScamDetector.Bot.Messaging;

public sealed class ReportCallbackHandler(IServiceScopeFactory scopeFactory)
{
    public async Task<string> HandleAsync(TelegramCallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (!ReportCallbackData.TryParse(callbackQuery.Data, out var label))
            return BotReplies.ReportInvalid;

        var originalText = callbackQuery.Message?.ReplyToMessage?.Content;
        if (originalText is null)
            return BotReplies.ReportOriginalMissing;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var reportService = scope.ServiceProvider.GetRequiredService<IMessageReportService>();
            var result = await reportService.SubmitAsync(originalText, label, ReportChannel.Telegram, cancellationToken);
            return result.IsSuccess ? BotReplies.ReportThanks : BotReplies.ReportInvalid;
        }
        catch (ReportsUnavailableException)
        {
            return BotReplies.ReportsUnavailable;
        }
    }
}
