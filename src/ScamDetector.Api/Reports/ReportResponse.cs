namespace ScamDetector.Api.Reports;

public sealed record ReportResponse(Guid Id, string Text, string Label, string Channel, DateTimeOffset CreatedAt);
