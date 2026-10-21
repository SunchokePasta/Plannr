using System.Net;
using System.Text;
using Plannr.Core.Capture;
using Plannr.Core.Exceptions;
using Plannr.Infrastructure.Options;
using Plannr.Infrastructure.Retrieval;

namespace Plannr.Tests.Retrieval;

public sealed class HttpPageFetcherTests
{
    private static readonly Uri Url = new("https://venue.example.com/event/1");

    private static HttpPageFetcher Fetcher(HttpMessageHandler handler, RetrievalOptions? options = null)
    {
        options ??= new RetrievalOptions();
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds) };
        return new HttpPageFetcher(client, options);
    }

    [Fact]
    public async Task Html_IsReturnedWithTheFinalUrl()
    {
        var finalUrl = new Uri("https://www.venue.example.com/events/thrift-market");

        var page = await Fetcher(StubHttpHandler.Html("<h1>Hi</h1>", finalUrl: finalUrl)).FetchAsync(Url);

        Assert.Equal("<h1>Hi</h1>", page.Html);
        Assert.Equal(finalUrl, page.FinalUrl);
        Assert.Equal(FetchMethod.Http, page.Method);
    }

    [Fact]
    public async Task NotFound_IsContentUnavailableNamingTheStatus()
    {
        var exception = await Assert.ThrowsAsync<ContentUnavailableException>(() =>
            Fetcher(StubHttpHandler.Html("gone", HttpStatusCode.NotFound)).FetchAsync(Url));

        Assert.Contains("404", exception.Message);
    }

    [Fact]
    public async Task APdf_IsContentUnavailable()
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]) { Headers = { { "Content-Type", "application/pdf" } } }
        }));

        var exception = await Assert.ThrowsAsync<ContentUnavailableException>(() => Fetcher(handler).FetchAsync(Url));

        Assert.Contains("application/pdf", exception.Message);
    }

    [Fact]
    public async Task ABodyOverTheLimit_IsContentUnavailable()
    {
        var big = new string('x', 150_000);

        await Assert.ThrowsAsync<ContentUnavailableException>(() =>
            Fetcher(StubHttpHandler.Html(big), new RetrievalOptions { MaxResponseBytes = 100_000 }).FetchAsync(Url));
    }

    [Fact]
    public async Task OurOwnTimeout_IsContentUnavailable()
    {
        var slow = new StubHttpHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var exception = await Assert.ThrowsAsync<ContentUnavailableException>(() =>
            Fetcher(slow, new RetrievalOptions { TimeoutSeconds = 1 }).FetchAsync(Url));

        Assert.Contains("timed out", exception.Message);
    }

    [Fact]
    public async Task CallerCancelling_IsPassedThroughNotWrapped()
    {
        using var cancel = new CancellationTokenSource();
        var slow = new StubHttpHandler(async (_, token) =>
        {
            await cancel.CancelAsync();
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Fetcher(slow).FetchAsync(Url, cancel.Token));
    }

    [Fact]
    public async Task ConnectionFailure_IsContentUnavailableWithTheCauseInside()
    {
        var refused = new StubHttpHandler((_, _) => throw new HttpRequestException("Connection refused"));

        var exception = await Assert.ThrowsAsync<ContentUnavailableException>(() => Fetcher(refused).FetchAsync(Url));

        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task NoContentType_IsStillReadAsHtml()
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("<p>hi</p>"))
        }));

        var page = await Fetcher(handler).FetchAsync(Url);

        Assert.Equal("<p>hi</p>", page.Html);
    }
}
