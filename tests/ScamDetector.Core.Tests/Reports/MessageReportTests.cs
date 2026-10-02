using ScamDetector.Core.Classification;
using ScamDetector.Core.Reports;

namespace ScamDetector.Core.Tests.Reports;

public sealed class MessageReportTests
{
    [Fact]
    public void Constructor_AllValues_ExposesThem()
    {
        var id = Guid.CreateVersion7();
        var createdAt = new DateTimeOffset(2026, 3, 1, 8, 30, 0, TimeSpan.Zero);

        var report = new MessageReport(id, "tin <PHONE>", MessageLabel.Spam, ReportChannel.Telegram, createdAt);

        Assert.Equal(id, report.Id);
        Assert.Equal("tin <PHONE>", report.MaskedText);
        Assert.Equal(MessageLabel.Spam, report.ReportedLabel);
        Assert.Equal(ReportChannel.Telegram, report.Channel);
        Assert.Equal(createdAt, report.CreatedAt);
    }
}
