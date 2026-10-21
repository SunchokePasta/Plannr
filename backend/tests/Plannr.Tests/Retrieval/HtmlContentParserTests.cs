using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Plannr.Core.Capture;
using Plannr.Infrastructure.Options;
using Plannr.Infrastructure.Retrieval;

namespace Plannr.Tests.Retrieval;

/// <summary>
/// Small hand-written pages, one behaviour each. Real saved pages from the
/// data pool are tested in change 8.
/// </summary>
public sealed class HtmlContentParserTests
{
    private static readonly Uri PageUrl = new("https://venue.example.com/whats-on/thrift-market");

    private static RetrievedContent Parse(string html, RetrievalOptions? options = null) =>
        new HtmlContentParser(options ?? new RetrievalOptions(), NullLogger<HtmlContentParser>.Instance)
            .Parse(html, PageUrl, SourceType.WebPage);

    [Fact]
    public void OpenGraphTags_AreReadFirst()
    {
        var content = Parse("""
            <html><head>
              <title>Page title | Venue</title>
              <meta property="og:title" content="Thrift Market">
              <meta property="og:description" content="Vintage stalls, Sat 14 Dec">
              <meta property="og:image" content="/images/thrift.jpg">
            </head><body></body></html>
            """);

        Assert.Equal("Thrift Market", content.Title);
        Assert.Equal("Vintage stalls, Sat 14 Dec", content.Description);
        Assert.Equal(new Uri("https://venue.example.com/images/thrift.jpg"), content.ThumbnailUrl);
    }

    [Fact]
    public void WithoutOpenGraph_FallsBackToTitleAndMetaDescription()
    {
        var content = Parse("""
            <html><head>
              <title>  Thrift   Market  </title>
              <meta name="description" content="Vintage stalls">
            </head><body></body></html>
            """);

        Assert.Equal("Thrift Market", content.Title);
        Assert.Equal("Vintage stalls", content.Description);
    }

    [Fact]
    public void EventInsideGraph_IsFoundAndMarksEventData()
    {
        var content = Parse("""
            <script type="application/ld+json">
            {"@context":"https://schema.org","@graph":[
              {"@type":"WebSite","name":"Venue"},
              {"@type":"MusicEvent","name":"Late Show","startDate":"2026-12-14T19:00"}
            ]}
            </script>
            """);

        Assert.True(content.HasEventData);
        var kept = Assert.Single(JsonNode.Parse(content.StructuredData!)!.AsArray());
        Assert.Equal("Late Show", kept!["name"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("""["Event","Festival"]""")]
    [InlineData("\"http://schema.org/ComedyEvent\"")]
    [InlineData("\"schema:Festival\"")]
    public void EventTypes_InAnyWritingStyle_AreRecognised(string type)
    {
        var content = Parse($$"""
            <script type="application/ld+json">{"@type":{{type}},"name":"X"}</script>
            """);

        Assert.True(content.HasEventData);
    }

    [Fact]
    public void PlaceWithoutEvent_IsKeptButIsNotEventData()
    {
        var content = Parse("""
            <script type="application/ld+json">
            {"@type":"Restaurant","name":"Dishoom","address":{"streetAddress":"7 Boundary St"}}
            </script>
            """);

        Assert.False(content.HasEventData);
        Assert.Contains("7 Boundary St", content.StructuredData);
    }

    [Fact]
    public void BrokenJsonLdBlock_IsSkippedAndTheRestIsRead()
    {
        var content = Parse("""
            <script type="application/ld+json">{"@type":"Event", "name": oops}</script>
            <script type="application/ld+json">{"@type":"Event","name":"The good one",}</script>
            """);

        Assert.True(content.HasEventData);
        Assert.Contains("The good one", content.StructuredData);
    }

    [Fact]
    public void PagesWithNoJsonLd_HaveNoStructuredData()
    {
        var content = Parse("<html><body><p>Just text</p></body></html>");

        Assert.Null(content.StructuredData);
        Assert.False(content.HasEventData);
    }

    [Fact]
    public void OverTheLimit_KeepsTheEventAndDropsThePlace()
    {
        var bigDescription = new string('x', 600);
        var content = Parse($$"""
            <script type="application/ld+json">{"@type":"Museum","name":"Venue","description":"{{bigDescription}}"}</script>
            <script type="application/ld+json">{"@type":"Event","name":"Show","description":"{{bigDescription}}"}</script>
            """, new RetrievalOptions { MaxStructuredDataChars = 700 });

        var kept = Assert.Single(JsonNode.Parse(content.StructuredData!)!.AsArray());
        Assert.Equal("Show", kept!["name"]!.GetValue<string>());
    }

    [Fact]
    public void BodyText_SkipsScriptsAndNavAndSeparatesBlocks()
    {
        var content = Parse("""
            <body>
              <nav>Home Events Shop</nav>
              <main><h1>Thrift Market</h1><p>Sat</p><p>7pm</p><script>track()</script></main>
              <footer>© Venue</footer>
            </body>
            """);

        Assert.Equal("Thrift Market Sat 7pm", content.BodyText);
    }

    [Fact]
    public void BodyText_IsCutAtAWordBoundary()
    {
        var words = string.Join(' ', Enumerable.Repeat("market", 200));

        var content = Parse($"<body><p>{words}</p></body>", new RetrievalOptions { MaxBodyTextChars = 500 });

        Assert.True(content.BodyText!.Length <= 500);
        Assert.EndsWith("market", content.BodyText);
    }
}
