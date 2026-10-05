namespace ScamDetector.Infrastructure.Llm;

public sealed record LlmModelOptions
{
    public const string SectionName = "LlmModel";

    public const string HttpClientName = "ollama";

    public const string DefaultModel = "qwen2.5:3b";

    public const int DefaultMaxOutputTokens = 64;

    public const string DefaultKeepAlive = "24h";

    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(60);

    public string BaseUrl { get; init; } = "";

    public string Model { get; init; } = DefaultModel;

    public TimeSpan RequestTimeout { get; init; } = DefaultRequestTimeout;

    public string KeepAlive { get; init; } = DefaultKeepAlive;

    public int MaxOutputTokens { get; init; } = DefaultMaxOutputTokens;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(BaseUrl);
}
