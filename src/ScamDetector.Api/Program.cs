using ScamDetector.Api.Classifications;
using ScamDetector.Api.DependencyInjection;
using ScamDetector.Api.ErrorHandling;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.Onnx;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
builder.Services.AddExceptionHandler<ScamModelUnavailableExceptionHandler>();
builder.Services.AddScamDetector(builder.Configuration, builder.Environment.ContentRootPath);

var app = builder.Build();

if (app.Services.GetRequiredService<IScamModel>() is UnavailableScamModel unavailableModel)
    app.Logger.LogWarning("Scam model is unavailable, missing files: {MissingFiles}", unavailableModel.MissingFiles);

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthChecks("/health");
app.MapClassificationEndpoints();

app.Run();
