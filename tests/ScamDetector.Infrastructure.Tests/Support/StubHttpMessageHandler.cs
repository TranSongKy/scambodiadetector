using System.Net;
using System.Text;

namespace ScamDetector.Infrastructure.Tests.Support;

public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public HttpRequestMessage? CapturedRequest { get; private set; }

    public string? CapturedBody { get; private set; }

    public static StubHttpMessageHandler Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        WithContentType(json, "application/json", statusCode);

    public static StubHttpMessageHandler WithContentType(
        string content,
        string mediaType,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(statusCode) { Content = new StringContent(content, Encoding.UTF8, mediaType) });

    public static StubHttpMessageHandler Throwing(Exception exception) => new(_ => throw exception);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CapturedRequest = request;
        CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return respond(request);
    }
}
