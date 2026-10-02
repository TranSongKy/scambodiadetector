using ScamDetector.Core.Reports;

namespace ScamDetector.Core.Tests.Reports;

public sealed class ReportsUnavailableExceptionTests
{
    [Fact]
    public void Constructor_MessageAndInnerException_KeepsBoth()
    {
        var inner = new InvalidOperationException("db down");

        var exception = new ReportsUnavailableException("unavailable", inner);

        Assert.Equal("unavailable", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void Constructor_MessageOnly_KeepsMessage()
    {
        var exception = new ReportsUnavailableException("unavailable");

        Assert.Equal("unavailable", exception.Message);
        Assert.Null(exception.InnerException);
    }
}
