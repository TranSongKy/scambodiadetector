using ScamDetector.Api.Classifications;
using ScamDetector.Api.ErrorHandling;
using ScamDetector.Api.Reports;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.DependencyInjection;
using ScamDetector.Infrastructure.HealthChecks;
using ScamDetector.Infrastructure.Onnx;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
builder.Services.AddExceptionHandler<ServiceUnavailableExceptionHandler>();
builder.Services.AddScamDetector(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddScamModelHealthCheck();
builder.Services.AddScamDetectorPersistence(builder.Configuration);

var app = builder.Build();

if (app.Services.GetRequiredService<IScamModel>() is UnavailableScamModel unavailableModel)
    app.Logger.LogWarning("Scam model is unavailable, missing files: {MissingFiles}", unavailableModel.MissingFiles);

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthChecks(HealthCheckRegistration.HealthRoute);
app.MapClassificationEndpoints();
app.MapReportEndpoints();

await app.Services.ApplyMigrationsIfConfiguredAsync(app.Lifetime.ApplicationStopping);
await app.RunAsync();
