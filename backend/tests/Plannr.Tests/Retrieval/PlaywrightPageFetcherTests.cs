using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Plannr.Core.Capture;
using Plannr.Core.Exceptions;
using Plannr.Infrastructure.Options;
using Plannr.Infrastructure.Retrieval;

namespace Plannr.Tests.Retrieval;

/// <summary>
/// Real headless Chromium against a real (local) web server. Slower than the
/// unit tests and needs Chromium installed, so it's tagged Integration: CI
/// runs it in its own step after installing the browser.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PlaywrightPageFetcherTests : IAsyncLifetime
{
    /// <summary>A page whose content only exists once its script runs, plus an image it asks for.</summary>
    private const string JavaScriptPage = """
        <html><body><div id="root"></div>
        <img src="/pixel.png">
        <script>
          setTimeout(() => {
            document.getElementById('root').innerHTML = '<h1>Thrift Market</h1><p>Sat 14 Dec</p>';
          }, 200);
        </script></body></html>
        """;

    private WebApplication _server = null!;
    private Uri _baseUrl = null!;
    private int _imageRequests;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _server = builder.Build();

        _server.MapGet("/js", () => Results.Content(JavaScriptPage, "text/html"));
        _server.MapGet("/pixel.png", () =>
        {
            Interlocked.Increment(ref _imageRequests);
            return Results.Bytes([0x89, 0x50, 0x4E, 0x47], "image/png");
        });
        _server.MapGet("/missing", () => Results.NotFound());

        await _server.StartAsync();
        var address = _server.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>()
            .Addresses.Single();
        _baseUrl = new Uri(address);
    }

    public async Task DisposeAsync() => await _server.DisposeAsync();

    private static PlaywrightPageFetcher Fetcher(bool allowEverything = true) =>
        new(new RetrievalOptions { BrowserSettleSeconds = 2 },
            NullLogger<PlaywrightPageFetcher>.Instance,
            (_, _) => ValueTask.FromResult(allowEverything));

    [Fact]
    public async Task RunsThePagesJavaScriptBeforeReadingIt()
    {
        await using var fetcher = Fetcher();

        var page = await fetcher.FetchAsync(new Uri(_baseUrl, "/js"));

        Assert.Equal(FetchMethod.Browser, page.Method);
        Assert.Contains("Thrift Market", page.Html);
    }

    [Fact]
    public async Task NeverDownloadsImages()
    {
        await using var fetcher = Fetcher();

        await fetcher.FetchAsync(new Uri(_baseUrl, "/js"));

        Assert.Equal(0, _imageRequests);
    }

    [Fact]
    public async Task A404_IsContentUnavailable()
    {
        await using var fetcher = Fetcher();

        var exception = await Assert.ThrowsAsync<ContentUnavailableException>(() =>
            fetcher.FetchAsync(new Uri(_baseUrl, "/missing")));

        Assert.Contains("404", exception.Message);
    }

    [Fact]
    public async Task APageTheFilterRefuses_IsContentUnavailable()
    {
        await using var fetcher = Fetcher(allowEverything: false);

        await Assert.ThrowsAsync<ContentUnavailableException>(() => fetcher.FetchAsync(new Uri(_baseUrl, "/js")));
    }

    [Fact]
    public async Task TheRealFilter_RefusesThisLocalServer()
    {
        // The public constructor uses the PublicAddressGuard check, and
        // 127.0.0.1 is exactly what it exists to refuse.
        await using var fetcher = new PlaywrightPageFetcher(new RetrievalOptions(), NullLogger<PlaywrightPageFetcher>.Instance);

        await Assert.ThrowsAsync<ContentUnavailableException>(() => fetcher.FetchAsync(new Uri(_baseUrl, "/js")));
    }
}
