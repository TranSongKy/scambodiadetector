using Microsoft.AspNetCore.Http.HttpResults;
using ScamDetector.Api.ErrorHandling;
using ScamDetector.Api.RateLimiting;
using ScamDetector.Core.Links;

namespace ScamDetector.Api.Links;

public static class LinkInspectionEndpoints
{
    public const string RoutePrefix = "/api/v1/link-inspections";

    public static IEndpointRouteBuilder MapLinkInspectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(RoutePrefix, InspectAsync).RequireRateLimiting(RateLimitOptions.PolicyName);
        return endpoints;
    }

    private static async Task<Results<Ok<InspectLinksResponse>, ProblemHttpResult>> InspectAsync(
        InspectLinksRequest request,
        ILinkInspectionService linkInspectionService,
        CancellationToken cancellationToken)
    {
        var result = await linkInspectionService.InspectAsync(request.Urls ?? [], cancellationToken);
        if (result.IsFailure)
            return DomainErrorProblems.ToProblem(result.Error!);

        return TypedResults.Ok(new InspectLinksResponse(result.Value
            .Select(inspection => new LinkInspectionResponse(
                inspection.Url,
                LinkVerdictNames.From(inspection.Verdict),
                inspection.Reasons))
            .ToList()));
    }
}
