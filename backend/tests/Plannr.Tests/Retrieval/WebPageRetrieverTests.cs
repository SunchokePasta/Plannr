using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Plannr.Core.Capture;
using Plannr.Core.Diagnostics;
using Plannr.Core.Exceptions;
using Plannr.Core.Interfaces;
using Plannr.Infrastructure.DependencyInjection;
using Plannr.Infrastructure.Options;
using Plannr.Infrastructure.Retrieval;
using Plannr.Tests.Diagnostics;

namespace Plannr.Tests.Retrieval;

/// <summary>Fakes for both fetchers, so these tests decide exactly what each "saw".</summary>
[Collection(ErrorReferenceCollection.Name)]
public sealed class WebPageRetrieverTests : IDisposable
{
    private const string EventPage = """
        <html><head><meta property="og:title" content="Thrift Market"></head>
        <body><script type="application/ld+json">{"@type":"Event","name":"Thrift Market"}</script>
        <main><p>Sat 14 Dec, 10am</p></main></body></html>
        """;

    /// <summary>What a JavaScript-built page looks like before its scripts run.</summary>
    private const string Shell = """
        <html><head><title>Dice</title></head><body><div id="root"></div><script src="/app.js"></script></body></html>
        """;

    private static readonly Uri Shared = new("https://bit.example/thrift");
    private static readonly Uri Final = new("https://venue.example.com/events/thrift-market");

    private readonly FakeLogger<WebPageRetriever> _logger = new();

    public WebPageRetrieverTests() => ErrorReference.Configure(ErrorReference.EvalProgram, ErrorReference.MainLog);

    public void Dispose() => ErrorReference.Reset();

    private sealed class FakeFetcher(Func<FetchedPage> fetch) : IBrowserPageFetcher
    {
        public int Calls { get; private set; }

        public Task<FetchedPage> FetchAsync(Uri url, CancellationToken cancellationToken = default)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(fetch());
        }

        public static FakeFetcher Returning(string html, FetchMethod method) =>
            new(() => new FetchedPage(Final, 200, html, method));
    }

    private WebPageRetriever Retriever(IPageFetcher http, IBrowserPageFetcher? browser) =>
        new(http,
            new HtmlContentParser(new RetrievalOptions(), NullLogger<HtmlContentParser>.Instance),
            new RetrievalOptions(),
            _logger,
            browser);

    [Fact]
    public async Task ARichPage_ParsesTheFinalUrlAndLogsOneSummaryLine()
    {
        var content = await Retriever(FakeFetcher.Returning(EventPage, FetchMethod.Http), browser: null)
            .RetrieveAsync(Shared);

        Assert.Equal(Final, content.SourceUrl);
        Assert.Equal(FetchMethod.Http, content.FetchedWith);
        Assert.True(content.HasEventData);

        var line = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, line.Level);
        Assert.Contains(Final.ToString(), line.Message);
    }

    [Fact]
    public async Task ARichPage_NeverStartsTheBrowser()
    {
        var browser = FakeFetcher.Returning(EventPage, FetchMethod.Browser);

        await Retriever(FakeFetcher.Returning(EventPage, FetchMethod.Http), browser).RetrieveAsync(Shared);

        Assert.Equal(0, browser.Calls);
    }

    [Fact]
    public async Task AJavaScriptShell_IsReloadedInTheBrowser()
    {
        var browser = FakeFetcher.Returning(EventPage, FetchMethod.Browser);

        var content = await Retriever(FakeFetcher.Returning(Shell, FetchMethod.Http), browser).RetrieveAsync(Shared);

        Assert.Equal(1, browser.Calls);
        Assert.Equal(FetchMethod.Browser, content.FetchedWith);
        Assert.True(content.HasEventData);
    }

    [Fact]
    public async Task ABrowserThatSawNoMore_LeavesTheHttpResult()
    {
        var browser = FakeFetcher.Returning(Shell, FetchMethod.Browser);

        var content = await Retriever(FakeFetcher.Returning(Shell, FetchMethod.Http), browser).RetrieveAsync(Shared);

        Assert.Equal(FetchMethod.Http, content.FetchedWith);
    }

    [Fact]
    public async Task ABrowserFailure_KeepsTheHttpResultAndWarnsWithAReference()
    {
        var browser = new FakeFetcher(() => throw new ContentUnavailableException("Browser GET timed out"));

        var content = await Retriever(FakeFetcher.Returning(Shell, FetchMethod.Http), browser).RetrieveAsync(Shared);

        Assert.Equal(FetchMethod.Http, content.FetchedWith);
        var warning = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.IsType<ContentUnavailableException>(warning.Exception);
        Assert.Matches(@"^\[[0-9a-f]{8}:03:01\]", warning.Message);
    }

    [Fact]
    public async Task CallerCancellingDuringTheBrowser_IsPassedThrough()
    {
        using var cancel = new CancellationTokenSource();
        var http = new FakeFetcher(() =>
        {
            cancel.Cancel();
            return new FetchedPage(Final, 200, Shell, FetchMethod.Http);
        });
        var browser = FakeFetcher.Returning(EventPage, FetchMethod.Browser);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Retriever(http, browser).RetrieveAsync(Shared, cancel.Token));
    }

    [Fact]
    public async Task WithoutABrowser_AShellIsReturnedAsItIs()
    {
        var content = await Retriever(FakeFetcher.Returning(Shell, FetchMethod.Http), browser: null)
            .RetrieveAsync(Shared);

        Assert.Equal(FetchMethod.Http, content.FetchedWith);
        Assert.Null(content.StructuredData);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddPlannrRetrieval_WiresEverythingUp(bool browserFallback)
    {
        var options = new ConfigurationBuilder().Build().GetValidated<RetrievalOptions>(RetrievalOptions.SectionName);
        options.BrowserFallbackEnabled = browserFallback;

        var services = new ServiceCollection().AddLogging().AddPlannrRetrieval(options);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Assert.IsType<LinkClassifier>(provider.GetRequiredService<ILinkClassifier>());
        Assert.Equal(SourceType.WebPage, provider.GetRequiredService<IContentRetriever>().Handles);
        Assert.Equal(browserFallback, provider.GetService<IBrowserPageFetcher>() is not null);
    }
}
