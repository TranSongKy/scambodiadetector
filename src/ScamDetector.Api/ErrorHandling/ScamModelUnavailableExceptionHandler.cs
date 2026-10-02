using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ScamDetector.Infrastructure.Onnx;

namespace ScamDetector.Api.ErrorHandling;

public sealed class ScamModelUnavailableExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private const string ModelUnavailableTitle = "Classification model is not available";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ScamModelUnavailableException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = ModelUnavailableTitle,
            },
        });
    }
}
