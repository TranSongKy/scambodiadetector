using System.Text.Json;

namespace ScamDetector.Infrastructure.Llm;

public sealed record OllamaChatRequest(
    string Model,
    IReadOnlyList<OllamaChatMessage> Messages,
    JsonElement Format,
    string KeepAlive,
    OllamaGenerationOptions Options,
    bool Stream = false);
