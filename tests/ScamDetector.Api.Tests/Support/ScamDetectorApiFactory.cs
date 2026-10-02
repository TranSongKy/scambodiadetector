using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScamDetector.Core.Classification;

namespace ScamDetector.Api.Tests.Support;

public sealed class ScamDetectorApiFactory(IScamModel? scamModel = null, IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            builder.UseSetting(key, value);

        if (scamModel is null)
            return;

        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(scamModel)));
    }
}
