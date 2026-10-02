namespace ScamDetector.Core.Reports;

public static class ReportListLimits
{
    public const int DefaultLimit = 100;
    public const int MaxLimit = 500;
    private const int MinLimit = 1;

    public static int Clamp(int? limit) => Math.Clamp(limit ?? DefaultLimit, MinLimit, MaxLimit);
}
