using System.Security.Cryptography;
using System.Text;

namespace ScamDetector.Api.Reports;

public sealed class ApiKeyEndpointFilter(ReportAdminOptions options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (string.IsNullOrEmpty(options.ApiKey))
            return TypedResults.NotFound();

        var providedKey = context.HttpContext.Request.Headers[ReportAdminOptions.ApiKeyHeaderName].ToString();
        if (!KeysMatch(providedKey, options.ApiKey))
            return TypedResults.Unauthorized();

        return await next(context);
    }

    private static bool KeysMatch(string providedKey, string expectedKey) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(providedKey)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey)));
}
