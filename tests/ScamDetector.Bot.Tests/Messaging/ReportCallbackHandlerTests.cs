using ScamDetector.Bot.Messaging;
using ScamDetector.Bot.Telegram;
using ScamDetector.Bot.Tests.Fakes;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Reports;

namespace ScamDetector.Bot.Tests.Messaging;

public sealed class ReportCallbackHandlerTests
{
    private static TelegramCallbackQuery CallbackFor(string? data, string? originalText)
    {
        var original = originalText is null ? null : new TelegramMessage(1, new TelegramChat(42), originalText, null);
        return new TelegramCallbackQuery("cb-1", new TelegramMessage(2, new TelegramChat(42), "bot reply", null, original), data);
    }

    [Fact]
    public async Task HandleAsync_ValidCallback_SubmitsOriginalTextWithTelegramChannel()
    {
        var reportService = new FakeMessageReportService();
        var handler = new ReportCallbackHandler(FakeReportServices.ScopeFactory(reportService));

        var answer = await handler.HandleAsync(CallbackFor(ReportCallbackData.For(MessageLabel.Spam), "tin goc"), CancellationToken.None);

        Assert.Equal(BotReplies.ReportThanks, answer);
        Assert.Equal(("tin goc", MessageLabel.Spam, ReportChannel.Telegram), Assert.Single(reportService.Submissions));
    }

    [Fact]
    public async Task HandleAsync_InvalidData_ReturnsInvalidWithoutSubmitting()
    {
        var reportService = new FakeMessageReportService();
        var handler = new ReportCallbackHandler(FakeReportServices.ScopeFactory(reportService));

        var answer = await handler.HandleAsync(CallbackFor("report:unknown", "tin goc"), CancellationToken.None);

        Assert.Equal(BotReplies.ReportInvalid, answer);
        Assert.Empty(reportService.Submissions);
    }

    [Fact]
    public async Task HandleAsync_OriginalMessageMissing_ReturnsOriginalMissing()
    {
        var reportService = new FakeMessageReportService();
        var handler = new ReportCallbackHandler(FakeReportServices.ScopeFactory(reportService));

        var answer = await handler.HandleAsync(CallbackFor(ReportCallbackData.For(MessageLabel.Scam), null), CancellationToken.None);

        Assert.Equal(BotReplies.ReportOriginalMissing, answer);
        Assert.Empty(reportService.Submissions);
    }

    [Fact]
    public async Task HandleAsync_ReportsUnavailable_ReturnsReportsUnavailable()
    {
        var handler = new ReportCallbackHandler(
            FakeReportServices.ScopeFactory(new FakeMessageReportService(new ReportsUnavailableException("no db"))));

        var answer = await handler.HandleAsync(CallbackFor(ReportCallbackData.For(MessageLabel.Scam), "tin goc"), CancellationToken.None);

        Assert.Equal(BotReplies.ReportsUnavailable, answer);
    }
}
