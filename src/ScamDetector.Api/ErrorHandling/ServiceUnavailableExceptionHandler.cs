using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ScamDetector.Core.Reports;
using ScamDetector.Infrastructure.Onnx;

namespace ScamDetector.Api.ErrorHandling;

public sealed class ServiceUnavailableExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private const string ModelUnavailableTitle = "Classification model is not available";
    private const string ReportsUnavailableTitle = "Report storage is not available";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var title = exception switch
        {
            ScamModelUnavailableException => ModelUnavailableTitle,
            ReportsUnavailableException => ReportsUnavailableTitle,
            _ => null,
        };
        if (title is null)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable, Title = title },
        });
    }
}
