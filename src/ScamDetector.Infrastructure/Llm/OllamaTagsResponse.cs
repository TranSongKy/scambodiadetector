namespace ScamDetector.Infrastructure.Llm;

public sealed record OllamaTagsResponse(IReadOnlyList<OllamaModelTag>? Models);
