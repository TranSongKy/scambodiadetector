using ScamDetector.Api.HealthChecks;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Urls;
using ScamDetector.Infrastructure.Onnx;

namespace ScamDetector.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private const string ModelHealthCheckName = "scam-model";

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
        services.AddHealthChecks().AddCheck<ScamModelHealthCheck>(ModelHealthCheckName);

        return services;
    }
}
