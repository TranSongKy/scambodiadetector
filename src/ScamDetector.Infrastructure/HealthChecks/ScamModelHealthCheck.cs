using Microsoft.Extensions.Diagnostics.HealthChecks;
using ScamDetector.Core.Classification;

namespace ScamDetector.Infrastructure.HealthChecks;

public sealed class ScamModelHealthCheck(IScamModel model) : IHealthCheck
{
    private const string NoModelReadyDescription =
        "No classification model is ready: PhoBERT files are missing and the LLM is disabled, unreachable or not pulled.";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken) =>
        await model.IsReadyAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(NoModelReadyDescription);
}
