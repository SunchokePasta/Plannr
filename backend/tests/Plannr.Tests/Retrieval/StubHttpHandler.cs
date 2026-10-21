using System.Net;
using System.Text;

namespace Plannr.Tests.Retrieval;

/// <summary>
/// Stands in for the network: answers every request with whatever the test
/// says, so fetcher tests never leave the machine.
/// </summary>
public sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        respond(request, cancellationToken);

    public static StubHttpHandler Html(string html, HttpStatusCode status = HttpStatusCode.OK, Uri? finalUrl = null) =>
        new((request, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),

            // What HttpClient sets after following redirects.
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUrl ?? request.RequestUri)
        }));
}
