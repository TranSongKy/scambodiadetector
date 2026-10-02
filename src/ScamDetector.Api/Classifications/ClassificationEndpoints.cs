using Microsoft.AspNetCore.Http.HttpResults;
using ScamDetector.Api.ErrorHandling;
using ScamDetector.Api.RateLimiting;
using ScamDetector.Core.Classification;

namespace ScamDetector.Api.Classifications;

public static class ClassificationEndpoints
{
    public const string RoutePrefix = "/api/v1/classifications";

    public static IEndpointRouteBuilder MapClassificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(RoutePrefix, ClassifyAsync).RequireRateLimiting(RateLimitOptions.PolicyName);
        return endpoints;
    }

    private static async Task<Results<Ok<ClassificationResponse>, ProblemHttpResult>> ClassifyAsync(
        ClassifyMessageRequest request,
        IMessageClassifier classifier,
        CancellationToken cancellationToken)
    {
        var result = await classifier.ClassifyAsync(request.Text ?? string.Empty, cancellationToken);
        if (result.IsFailure)
            return DomainErrorProblems.ToProblem(result.Error!);

        var classification = result.Value;
        return TypedResults.Ok(new ClassificationResponse(
            MessageLabelNames.From(classification.Label),
            classification.Confidence,
            classification.Reasons));
    }
}
