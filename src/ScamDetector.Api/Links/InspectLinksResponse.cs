namespace ScamDetector.Api.Links;

public sealed record InspectLinksResponse(IReadOnlyList<LinkInspectionResponse> Results);
