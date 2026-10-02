using Microsoft.AspNetCore.Http.HttpResults;
using ScamDetector.Api.ErrorHandling;
using ScamDetector.Api.RateLimiting;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Reports;

namespace ScamDetector.Api.Reports;

public static class ReportEndpoints
{
    public const string RoutePrefix = "/api/v1/reports";

    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(RoutePrefix, CreateAsync).RequireRateLimiting(RateLimitOptions.PolicyName);
        return endpoints;
    }

    private static async Task<Results<Created<CreateReportResponse>, ProblemHttpResult>> CreateAsync(
        CreateReportRequest request,
        IMessageReportService reportService,
        CancellationToken cancellationToken)
    {
        if (!MessageLabelNames.TryParse(request.Label, out var label))
            return DomainErrorProblems.ToProblem(ReportErrors.InvalidLabel);

        var channel = ReportChannel.Api;
        if (request.Channel is not null && !ReportChannelNames.TryParse(request.Channel, out channel))
            return DomainErrorProblems.ToProblem(ReportErrors.InvalidChannel);

        var result = await reportService.SubmitAsync(request.Text ?? string.Empty, label, channel, cancellationToken);
        if (result.IsFailure)
            return DomainErrorProblems.ToProblem(result.Error!);

        return TypedResults.Created($"{RoutePrefix}/{result.Value}", new CreateReportResponse(result.Value));
    }
}
