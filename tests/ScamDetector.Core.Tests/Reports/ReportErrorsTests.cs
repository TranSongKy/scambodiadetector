using ScamDetector.Core.Reports;

namespace ScamDetector.Core.Tests.Reports;

public sealed class ReportErrorsTests
{
    [Fact]
    public void InvalidLabel_Always_HasStableCode()
    {
        Assert.Equal("report.invalid_label", ReportErrors.InvalidLabel.Code);
    }

    [Fact]
    public void InvalidChannel_Always_HasStableCode()
    {
        Assert.Equal("report.invalid_channel", ReportErrors.InvalidChannel.Code);
    }
}
