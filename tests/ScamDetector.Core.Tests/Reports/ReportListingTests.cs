using ScamDetector.Core.Reports;
using ScamDetector.Core.Tests.Fakes;

namespace ScamDetector.Core.Tests.Reports;

public sealed class ReportListingTests
{
    private static readonly DateTimeOffset Since = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null, ReportListLimits.DefaultLimit)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(50, 50)]
    [InlineData(10_000, ReportListLimits.MaxLimit)]
    public void Clamp_AnyLimit_ReturnsValueWithinBounds(int? requested, int expected)
    {
        var clamped = ReportListLimits.Clamp(requested);

        Assert.Equal(expected, clamped);
    }

    [Fact]
    public async Task ListAsync_RequestedLimitAboveMax_PassesClampedLimitAndSinceToRepository()
    {
        var repository = new FakeMessageReportRepository();
        var service = new MessageReportService(repository, TimeProvider.System);

        await service.ListAsync(Since, 9_999, CancellationToken.None);

        Assert.Equal((Since, ReportListLimits.MaxLimit), repository.ReceivedListQuery);
    }

    [Theory]
    [InlineData(ReportChannel.Api, ReportChannelNames.Api)]
    [InlineData(ReportChannel.Telegram, ReportChannelNames.Telegram)]
    [InlineData(ReportChannel.Extension, ReportChannelNames.Extension)]
    public void ReportChannelNamesFrom_EachChannel_ReturnsLowercaseName(ReportChannel channel, string expected)
    {
        Assert.Equal(expected, ReportChannelNames.From(channel));
    }
}
