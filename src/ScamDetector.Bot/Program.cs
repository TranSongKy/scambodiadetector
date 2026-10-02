using ScamDetector.Bot.Messaging;
using ScamDetector.Bot.Polling;
using ScamDetector.Bot.Telegram;
using ScamDetector.Infrastructure.DependencyInjection;
using ScamDetector.Infrastructure.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var telegramOptions = TelegramOptionsValidator.Validate(
    builder.Configuration.GetSection(TelegramOptions.SectionName).Get<TelegramOptions>() ?? new TelegramOptions());
var httpTimeout = TimeSpan.FromSeconds(telegramOptions.PollingTimeoutSeconds * 2);

builder.Services.AddScamDetector(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddScamModelHealthCheck();
builder.Services.AddSingleton(telegramOptions);
builder.Services.AddSingleton<BotReplyBuilder>();
builder.Services.AddHttpClient<ITelegramClient, TelegramApiClient>(client =>
{
    client.BaseAddress = TelegramOptionsValidator.BotApiAddress(telegramOptions);
    client.Timeout = httpTimeout;
});
builder.Services.AddHostedService<TelegramPollingService>();

var app = builder.Build();

app.MapHealthChecks(HealthCheckRegistration.HealthRoute);

app.Run();
