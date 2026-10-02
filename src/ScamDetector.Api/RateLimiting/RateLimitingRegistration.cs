using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

namespace ScamDetector.Api.RateLimiting;

public static class RateLimitingRegistration
{
    private const string UnknownClient = "unknown";

    public static IServiceCollection AddClientRateLimiting(this IServiceCollection services, RateLimitOptions options)
    {
        if (options.PermitLimit <= 0 || options.WindowSeconds <= 0)
            throw new InvalidOperationException($"{RateLimitOptions.SectionName} limits must be positive.");

        services.Configure<ForwardedHeadersOptions>(forwarded =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwarded.KnownIPNetworks.Clear();
            forwarded.KnownProxies.Clear();
        });
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(RateLimitOptions.PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.WindowSeconds),
                    QueueLimit = 0,
                }));
        });
        return services;
    }

    public static WebApplication UseClientRateLimiting(this WebApplication app, RateLimitOptions options)
    {
        if (options.TrustForwardedHeaders)
            app.UseForwardedHeaders();
        app.UseRateLimiter();
        return app;
    }
}
