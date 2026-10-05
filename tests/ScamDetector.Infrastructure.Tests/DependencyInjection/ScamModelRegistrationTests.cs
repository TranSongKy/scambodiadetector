using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.DependencyInjection;
using ScamDetector.Infrastructure.Llm;

namespace ScamDetector.Infrastructure.Tests.DependencyInjection;

public sealed class ScamModelRegistrationTests
{
    private const string MissingModelPath = "missing/scam-detector.onnx";

    [Fact]
    public void AddScamDetector_NoPhoBertAndLlmDisabled_RegistersUnavailableModel()
    {
        using var provider = BuildProvider(llmBaseUrl: "");

        var model = provider.GetRequiredService<IScamModel>();

        Assert.IsType<FallbackScamModel>(model);
        Assert.False(model.IsAvailable);
    }

    [Fact]
    public void AddScamDetector_NoPhoBertAndLlmEnabled_RegistersAvailableModel()
    {
        using var provider = BuildProvider(llmBaseUrl: "http://ollama.test:11434");

        var model = provider.GetRequiredService<IScamModel>();

        Assert.True(model.IsAvailable);
    }

    [Fact]
    public void AddScamDetector_LlmEnabled_ConfiguresNamedClientFromOptions()
    {
        using var provider = BuildProvider(llmBaseUrl: "http://ollama.test:11434");

        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(LlmModelOptions.HttpClientName);

        Assert.Equal(new Uri("http://ollama.test:11434"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(45), client.Timeout);
    }

    private static ServiceProvider BuildProvider(string llmBaseUrl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OnnxModel:ModelPath"] = MissingModelPath,
                ["LlmModel:BaseUrl"] = llmBaseUrl,
                ["LlmModel:RequestTimeout"] = "00:00:45",
            })
            .Build();
        return new ServiceCollection()
            .AddScamDetector(configuration, AppContext.BaseDirectory)
            .BuildServiceProvider();
    }
}
