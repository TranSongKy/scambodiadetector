using System.Net;
using System.Text;

namespace ScamDetector.Bot.Tests.Fakes;

public sealed class FakeHttpMessageHandler(string responseJson) : HttpMessageHandler
{
    public HttpRequestMessage? CapturedRequest { get; private set; }

    public string? CapturedBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CapturedRequest = request;
        CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        };
    }
}
