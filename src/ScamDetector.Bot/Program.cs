using ScamDetector.Bot.Messaging;
using ScamDetector.Bot.Polling;
using ScamDetector.Bot.Telegram;
using ScamDetector.Infrastructure.DependencyInjection;
using ScamDetector.Infrastructure.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var telegramOptions = TelegramOptionsValidator.Validate(
    builder.Configuration.GetSection(TelegramOptions.SectionName).Get<TelegramOptions>() ?? new TelegramOptions());

builder.Services.AddScamDetector(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddScamModelHealthCheck();
builder.Services.AddSingleton(telegramOptions);
builder.Services.AddScamDetectorPersistence(builder.Configuration);
builder.Services.AddSingleton<BotReplyBuilder>();
builder.Services.AddSingleton<ReportCallbackHandler>();
builder.Services
    .AddHttpClient<ITelegramClient, TelegramApiClient>(client =>
    {
        client.BaseAddress = TelegramOptionsValidator.BotApiAddress(telegramOptions);
        client.Timeout = telegramOptions.HttpTimeout;
    })
    .RemoveAllLoggers();
builder.Services.AddHostedService<TelegramPollingService>();

var app = builder.Build();

app.MapHealthChecks(HealthCheckRegistration.HealthRoute);

app.Run();
