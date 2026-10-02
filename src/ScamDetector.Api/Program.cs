using ScamDetector.Api.Classifications;
using ScamDetector.Api.DependencyInjection;
using ScamDetector.Api.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ScamModelUnavailableExceptionHandler>();
builder.Services.AddScamDetector(builder.Configuration, builder.Environment.ContentRootPath);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthChecks("/health");
app.MapClassificationEndpoints();

app.Run();
