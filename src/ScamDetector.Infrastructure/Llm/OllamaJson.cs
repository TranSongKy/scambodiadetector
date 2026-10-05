using System.Text.Json;

namespace ScamDetector.Infrastructure.Llm;

public static class OllamaJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
