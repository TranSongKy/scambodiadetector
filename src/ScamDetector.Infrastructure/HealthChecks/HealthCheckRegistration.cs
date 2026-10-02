using Microsoft.Extensions.DependencyInjection;

namespace ScamDetector.Infrastructure.HealthChecks;

public static class HealthCheckRegistration
{
    public const string HealthRoute = "/health";
    private const string ModelHealthCheckName = "scam-model";

    public static IServiceCollection AddScamModelHealthCheck(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<ScamModelHealthCheck>(ModelHealthCheckName);
        return services;
    }
}
