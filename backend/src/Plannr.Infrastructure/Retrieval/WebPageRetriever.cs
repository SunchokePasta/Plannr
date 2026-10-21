using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Plannr.Core.Capture;
using Plannr.Core.Diagnostics;
using Plannr.Core.Interfaces;
using Plannr.Infrastructure.Options;

namespace Plannr.Infrastructure.Retrieval;

/// <summary>
/// Event, ticket and general web pages: fetch the HTML over plain HTTP and
/// read it with <see cref="HtmlContentParser"/>. Only when that comes back
/// as a JavaScript shell -- no JSON-LD and almost no text -- is the page
/// loaded again in a real browser.
///
/// HTTP failures are not caught here: they are ContentUnavailableExceptions
/// already, and are logged once, with a reference, by whoever runs the
/// pipeline (Worker or Eval). A browser failure *is* caught, because it
/// changes the outcome: the HTTP result is still a usable, if thin, page.
/// </summary>
public sealed class WebPageRetriever(
    IPageFetcher httpFetcher,
    HtmlContentParser parser,
    RetrievalOptions options,
    ILogger<WebPageRetriever> logger,
    IBrowserPageFetcher? browserFetcher = null) : IContentRetriever
{
    public SourceType Handles => SourceType.WebPage;

    public async Task<RetrievedContent> RetrieveAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var page = await httpFetcher.FetchAsync(url, cancellationToken);
        var content = Read(page);

        if (browserFetcher is not null && NeedsBrowser(content))
        {
            logger.LogInformation(
                "{Url} looks JavaScript-rendered (no JSON-LD, {BodyChars} chars of text); loading it in the browser.",
                page.FinalUrl, content.BodyText?.Length ?? 0);

            (page, content) = await TryBrowserAsync(url, page, content, cancellationToken);
        }

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

    /// <summary>
    /// The page has nothing structured and next to no text. That is what a
    /// JavaScript-built page looks like before its scripts run. Deliberately
    /// not triggered by a 403: retrying a site that refused us, in a real
    /// browser, would be getting round its decision.
    /// </summary>
    private bool NeedsBrowser(RetrievedContent content) =>
        content.StructuredData is null
        && (content.BodyText?.Length ?? 0) < options.MinBodyTextChars;

    private async Task<(FetchedPage Page, RetrievedContent Content)> TryBrowserAsync(
        Uri url, FetchedPage httpPage, RetrievedContent httpContent, CancellationToken cancellationToken)
    {
        try
        {
            var browserPage = await browserFetcher!.FetchAsync(url, cancellationToken);
            var browserContent = Read(browserPage);

            // Keep whichever read better. A browser that saw no more than
            // HTTP did isn't worth reporting as the source.
            var better = browserContent.StructuredData is not null
                || (browserContent.BodyText?.Length ?? 0) > (httpContent.BodyText?.Length ?? 0);

            return better ? (browserPage, browserContent) : (httpPage, httpContent);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var reference = ErrorReference.New();
            logger.LogWarning(
                exception,
                "[{Reference}] The browser couldn't load {Url}; keeping the plain HTTP result.",
                reference, url);

            return (httpPage, httpContent);
        }
    }

    private RetrievedContent Read(FetchedPage page) =>
        parser.Parse(page.Html, page.FinalUrl, SourceType.WebPage) with { FetchedWith = page.Method };
}
