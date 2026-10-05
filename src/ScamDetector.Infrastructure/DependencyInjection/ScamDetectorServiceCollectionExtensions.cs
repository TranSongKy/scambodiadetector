using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScamDetector.Core.Classification;
using ScamDetector.Core.ThreatIntel;
using ScamDetector.Core.Urls;
using ScamDetector.Infrastructure.Llm;
using ScamDetector.Infrastructure.Onnx;
using ScamDetector.Infrastructure.ThreatIntel;

namespace ScamDetector.Infrastructure.DependencyInjection;

public static class ScamDetectorServiceCollectionExtensions
{
    public static IServiceCollection AddScamDetector(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var classificationOptions = ClassificationOptionsValidator.Validate(
            configuration.GetSection(ClassificationOptions.SectionName).Get<ClassificationOptions>()
            ?? new ClassificationOptions());
        var onnxModelOptions = configuration.GetSection(OnnxModelOptions.SectionName).Get<OnnxModelOptions>()
            ?? new OnnxModelOptions();

        services.AddSingleton(classificationOptions);
        services.AddSingleton(onnxModelOptions);
        var threatIntelOptions = configuration.GetSection(ThreatIntelOptions.SectionName).Get<ThreatIntelOptions>()
            ?? new ThreatIntelOptions();
        services.AddSingleton(threatIntelOptions);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IThreatIntelligence>(provider => new ReloadingThreatIntelligence(
            Path.Combine(contentRootPath, threatIntelOptions.DataDirectory),
            threatIntelOptions,
            provider.GetRequiredService<TimeProvider>()));
        var llmModelOptions = configuration.GetSection(LlmModelOptions.SectionName).Get<LlmModelOptions>()
            ?? new LlmModelOptions();
        services.AddSingleton(llmModelOptions);
        if (llmModelOptions.IsEnabled)
            services.AddOllamaHttpClient(llmModelOptions);
        services.AddSingleton<IScamModel>(provider => new FallbackScamModel(
            CreateModelChain(provider, onnxModelOptions, llmModelOptions, contentRootPath)));
        services.AddSingleton<IUrlInspector, RuleBasedUrlInspector>();
        services.AddSingleton<IMessageClassifier, MessageClassifier>();

        return services;
    }

    private static void AddOllamaHttpClient(this IServiceCollection services, LlmModelOptions llmModelOptions) =>
        services
            .AddHttpClient(LlmModelOptions.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(llmModelOptions.BaseUrl, UriKind.Absolute);
                client.Timeout = llmModelOptions.RequestTimeout;
            })
            .RemoveAllLoggers();

    private static List<IScamModel> CreateModelChain(
        IServiceProvider provider,
        OnnxModelOptions onnxModelOptions,
        LlmModelOptions llmModelOptions,
        string contentRootPath)
    {
        List<IScamModel> models = [ScamModelFactory.Create(onnxModelOptions, contentRootPath)];
        if (llmModelOptions.IsEnabled)
            models.Add(new OllamaScamModel(provider.GetRequiredService<IHttpClientFactory>(), llmModelOptions));
        return models;
    }
}
