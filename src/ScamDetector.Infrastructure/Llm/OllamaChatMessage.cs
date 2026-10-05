namespace ScamDetector.Infrastructure.Llm;

public sealed record OllamaChatMessage(string Role, string Content)
{
    public const string SystemRole = "system";
    public const string UserRole = "user";
}
