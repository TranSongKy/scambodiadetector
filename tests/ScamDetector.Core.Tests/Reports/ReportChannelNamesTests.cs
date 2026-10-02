using ScamDetector.Core.Reports;

namespace ScamDetector.Core.Tests.Reports;

public sealed class ReportChannelNamesTests
{
    [Theory]
    [InlineData("api", ReportChannel.Api)]
    [InlineData("telegram", ReportChannel.Telegram)]
    [InlineData("extension", ReportChannel.Extension)]
    [InlineData("API", ReportChannel.Api)]
    [InlineData("Telegram", ReportChannel.Telegram)]
    [InlineData("  extension  ", ReportChannel.Extension)]
    public void TryParse_KnownNameAnyCaseOrPadded_ReturnsTrueWithChannel(string name, ReportChannel expected)
    {
        var parsed = ReportChannelNames.TryParse(name, out var channel);

        Assert.True(parsed);
        Assert.Equal(expected, channel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("web")]
    [InlineData("tele gram")]
    [InlineData("0")]
    public void TryParse_UnknownOrEmptyName_ReturnsFalse(string name)
    {
        var parsed = ReportChannelNames.TryParse(name, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParse_NullName_ReturnsFalse()
    {
        var parsed = ReportChannelNames.TryParse(null, out _);

        Assert.False(parsed);
    }
}
