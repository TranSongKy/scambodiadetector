namespace ScamDetector.Api.Tests.Support;

public sealed record ProblemResponse(int Status, string? Title, string? Detail, string? Code);
