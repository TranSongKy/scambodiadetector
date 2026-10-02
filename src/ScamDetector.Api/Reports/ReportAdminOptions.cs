namespace ScamDetector.Api.Reports;

public sealed record ReportAdminOptions
{
    public const string SectionName = "ReportAdmin";
    public const string ApiKeyHeaderName = "X-Api-Key";

    public string ApiKey { get; init; } = string.Empty;
}
