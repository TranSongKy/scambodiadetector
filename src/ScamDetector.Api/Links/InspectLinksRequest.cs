namespace ScamDetector.Api.Links;

public sealed record InspectLinksRequest(IReadOnlyList<string>? Urls);
