using Plannr.Core.Capture;

namespace Plannr.Infrastructure.Retrieval;

/// <summary>A page's HTML and where it really came from, after redirects.</summary>
public sealed record FetchedPage(Uri FinalUrl, int StatusCode, string Html, FetchMethod Method);

/// <summary>
/// Gets one page's HTML. An Infrastructure detail of WebPageRetriever (and,
/// later, TikTok's page scraper), not a Core interface: the pipeline only
/// cares about the RetrievedContent that comes out.
/// </summary>
public interface IPageFetcher
{
    /// <exception cref="Core.Exceptions.ContentUnavailableException">
    /// Not found, blocked, not HTML, too large, or timed out.
    /// </exception>
    /// <exception cref="OperationCanceledException">The caller cancelled; passed through untouched.</exception>
    Task<FetchedPage> FetchAsync(Uri url, CancellationToken cancellationToken = default);
}
