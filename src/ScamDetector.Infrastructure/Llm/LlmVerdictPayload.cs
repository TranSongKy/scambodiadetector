namespace ScamDetector.Infrastructure.Llm;

public sealed record LlmVerdictPayload(string? Label, double? Confidence);
