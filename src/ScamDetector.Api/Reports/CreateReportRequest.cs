namespace ScamDetector.Api.Reports;

public sealed record CreateReportRequest(string? Text, string? Label, string? Channel);
