using Microsoft.Extensions.DependencyInjection;
using ScamDetector.Core.Reports;

namespace ScamDetector.Bot.Tests.Fakes;

public static class FakeReportServices
{
    public static IServiceScopeFactory ScopeFactory(IMessageReportService reportService) =>
        new ServiceCollection()
            .AddScoped(_ => reportService)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
}
