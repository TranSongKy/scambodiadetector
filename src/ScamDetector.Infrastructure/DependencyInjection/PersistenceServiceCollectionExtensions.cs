using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScamDetector.Core.Reports;
using ScamDetector.Infrastructure.HealthChecks;
using ScamDetector.Infrastructure.Persistence;

namespace ScamDetector.Infrastructure.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    private const string DatabaseHealthCheckName = "database";

    public static IServiceCollection AddScamDetectorPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IMessageReportService, MessageReportService>();
        services.AddSingleton(configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions());

        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IMessageReportRepository, UnavailableMessageReportRepository>();
            return services;
        }

        services.AddDbContext<ScamDetectorDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IMessageReportRepository, EfMessageReportRepository>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>(DatabaseHealthCheckName);
        return services;
    }

    public static async Task ApplyMigrationsIfConfiguredAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!services.GetRequiredService<DatabaseOptions>().ApplyMigrationsOnStartup)
            return;

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetService<ScamDetectorDbContext>();
        if (dbContext is not null)
            await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
