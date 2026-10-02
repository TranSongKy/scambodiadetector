namespace ScamDetector.Core.Reports;

public sealed class ReportsUnavailableException : Exception
{
    public ReportsUnavailableException()
    {
    }

    public ReportsUnavailableException(string message)
        : base(message)
    {
    }

    public ReportsUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
