using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScamDetector.Core.Classification;
using ScamDetector.Core.ThreatIntel;
using ScamDetector.Core.Urls;
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
        services.AddSingleton(_ => ScamModelFactory.Create(onnxModelOptions, contentRootPath));
        services.AddSingleton<IUrlInspector, RuleBasedUrlInspector>();
        services.AddSingleton<IMessageClassifier, MessageClassifier>();

        return services;
    }
}
