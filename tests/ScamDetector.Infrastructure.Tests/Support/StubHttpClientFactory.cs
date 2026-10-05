namespace ScamDetector.Infrastructure.Tests.Support;

public sealed class StubHttpClientFactory(HttpMessageHandler handler, Uri baseAddress) : IHttpClientFactory
{
    public string? RequestedName { get; private set; }

    public HttpClient CreateClient(string name)
    {
        RequestedName = name;
        return new HttpClient(handler, disposeHandler: false) { BaseAddress = baseAddress };
    }
}
