using Microsoft.AspNetCore.Http.HttpResults;
using ScamDetector.Core.Common;

namespace ScamDetector.Api.ErrorHandling;

public static class DomainErrorProblems
{
    public const string ErrorCodeKey = "code";
    private const string InvalidRequestTitle = "Invalid request";

    public static ProblemHttpResult ToProblem(DomainError error) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: InvalidRequestTitle,
            detail: error.Message,
            extensions: new Dictionary<string, object?> { [ErrorCodeKey] = error.Code });
}
