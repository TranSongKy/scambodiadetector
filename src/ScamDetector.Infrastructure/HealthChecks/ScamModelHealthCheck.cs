using Microsoft.Extensions.Diagnostics.HealthChecks;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.Onnx;

namespace ScamDetector.Infrastructure.HealthChecks;

public sealed class ScamModelHealthCheck(IScamModel model) : IHealthCheck
{
    private const string ModelMissingDescription = "Model files are missing.";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken) =>
        Task.FromResult(model is UnavailableScamModel
            ? HealthCheckResult.Unhealthy(ModelMissingDescription)
            : HealthCheckResult.Healthy());
}
