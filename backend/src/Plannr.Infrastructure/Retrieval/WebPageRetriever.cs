using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Plannr.Core.Capture;
using Plannr.Core.Interfaces;

namespace Plannr.Infrastructure.Retrieval;

/// <summary>
/// Event, ticket and general web pages: fetch the HTML, read it with
/// <see cref="HtmlContentParser"/>. Failures are not caught here -- they
/// are ContentUnavailableExceptions already, and are logged once, with a
/// reference, by whoever runs the pipeline (Worker or Eval).
/// </summary>
public sealed class WebPageRetriever(
    IPageFetcher httpFetcher,
    HtmlContentParser parser,
    ILogger<WebPageRetriever> logger) : IContentRetriever
{
    public SourceType Handles => SourceType.WebPage;

    public async Task<RetrievedContent> RetrieveAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var page = await httpFetcher.FetchAsync(url, cancellationToken);
        var content = parser.Parse(page.Html, page.FinalUrl, SourceType.WebPage) with { FetchedWith = page.Method };

        logger.LogInformation(
            "Retrieved {Url} via {Method} in {ElapsedMs} ms: {StatusCode}, {HtmlChars} chars of HTML, " +
            "event data {HasEventData}, {StructuredChars} chars of JSON-LD, {BodyChars} chars of text.",
            page.FinalUrl,
            page.Method,
            stopwatch.ElapsedMilliseconds,
            page.StatusCode,
            page.Html.Length,
            content.HasEventData,
            content.StructuredData?.Length ?? 0,
            content.BodyText?.Length ?? 0);

        return content;
    }
}
