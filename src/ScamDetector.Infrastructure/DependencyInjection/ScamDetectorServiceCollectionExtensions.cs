using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Urls;
using ScamDetector.Infrastructure.Onnx;

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
        services.AddSingleton(_ => ScamModelFactory.Create(onnxModelOptions, contentRootPath));
        services.AddSingleton<IUrlInspector, RuleBasedUrlInspector>();
        services.AddSingleton<IMessageClassifier, MessageClassifier>();

        return services;
    }
}
