using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Plannr.Core.Capture;
using Plannr.Core.Interfaces;
using Plannr.Infrastructure.DependencyInjection;
using Plannr.Infrastructure.Options;
using Plannr.Infrastructure.Retrieval;

namespace Plannr.Tests.Retrieval;

public sealed class WebPageRetrieverTests
{
    private const string EventPage = """
        <html><head><meta property="og:title" content="Thrift Market"></head>
        <body><script type="application/ld+json">{"@type":"Event","name":"Thrift Market"}</script>
        <main><p>Sat 14 Dec, 10am</p></main></body></html>
        """;

    private static readonly Uri Shared = new("https://bit.example/thrift");
    private static readonly Uri Final = new("https://venue.example.com/events/thrift-market");

    private sealed class FakeFetcher(FetchedPage page) : IPageFetcher
    {
        public Task<FetchedPage> FetchAsync(Uri url, CancellationToken cancellationToken = default) => Task.FromResult(page);
    }

    [Fact]
    public async Task Retrieve_ParsesTheFinalPageAndLogsOneSummaryLine()
    {
        var logger = new FakeLogger<WebPageRetriever>();
        var retriever = new WebPageRetriever(
            new FakeFetcher(new FetchedPage(Final, 200, EventPage, FetchMethod.Http)),
            new HtmlContentParser(new RetrievalOptions(), NullLogger<HtmlContentParser>.Instance),
            logger);

        var content = await retriever.RetrieveAsync(Shared);

        Assert.Equal(Final, content.SourceUrl);
        Assert.Equal(FetchMethod.Http, content.FetchedWith);
        Assert.True(content.HasEventData);
        Assert.Equal("Thrift Market", content.Title);

        var line = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, line.Level);
        Assert.Contains(Final.ToString(), line.Message);
    }

    [Fact]
    public void AddPlannrRetrieval_WiresEverythingUp()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddPlannrRetrieval(new ConfigurationBuilder().Build().GetValidated<RetrievalOptions>(RetrievalOptions.SectionName));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Assert.IsType<LinkClassifier>(provider.GetRequiredService<ILinkClassifier>());
        Assert.Equal(SourceType.WebPage, provider.GetRequiredService<IContentRetriever>().Handles);
    }
}
