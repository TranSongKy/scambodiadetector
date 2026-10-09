namespace ScamDetector.Api.Links;

public sealed record LinkInspectionResponse(string Url, string Verdict, IReadOnlyList<string> Reasons);
