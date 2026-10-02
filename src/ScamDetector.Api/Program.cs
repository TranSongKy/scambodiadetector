using ScamDetector.Api.Classifications;
using ScamDetector.Api.ErrorHandling;
using ScamDetector.Api.RateLimiting;
using ScamDetector.Api.Reports;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.DependencyInjection;
using ScamDetector.Infrastructure.HealthChecks;
using ScamDetector.Infrastructure.Onnx;

var builder = WebApplication.CreateBuilder(args);
var rateLimitOptions = builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
builder.Services.AddExceptionHandler<ServiceUnavailableExceptionHandler>();
builder.Services.AddScamDetector(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddScamModelHealthCheck();
builder.Services.AddScamDetectorPersistence(builder.Configuration);
builder.Services.AddClientRateLimiting(rateLimitOptions);
builder.Services.AddSingleton(builder.Configuration.GetSection(ReportAdminOptions.SectionName).Get<ReportAdminOptions>() ?? new ReportAdminOptions());

var app = builder.Build();

if (app.Services.GetRequiredService<IScamModel>() is UnavailableScamModel unavailableModel)
    app.Logger.LogWarning("Scam model is unavailable, missing files: {MissingFiles}", unavailableModel.MissingFiles);

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseClientRateLimiting(rateLimitOptions);

app.MapHealthChecks(HealthCheckRegistration.HealthRoute);
app.MapClassificationEndpoints();
app.MapReportEndpoints();

await app.Services.ApplyMigrationsIfConfiguredAsync(app.Lifetime.ApplicationStopping);
await app.RunAsync();
