using Plannr.Core.Capture;
using Plannr.Core.Exceptions;
using Plannr.Infrastructure.Options;

namespace Plannr.Infrastructure.Retrieval;

/// <summary>
/// A plain GET. The HttpClient it is given is configured in
/// AddPlannrRetrieval: timeout, User-Agent, redirect limit, and
/// <see cref="PublicAddressGuard"/> on every connection.
///
/// Every failure becomes a ContentUnavailableException whose Message says
/// what happened (for the log) -- except the caller cancelling, which is
/// passed through untouched so the boundary can tell "they hung up" from
/// "it broke".
/// </summary>
public sealed class HttpPageFetcher(HttpClient httpClient, RetrievalOptions options) : IPageFetcher
{
    private static readonly string[] HtmlMediaTypes = ["text/html", "application/xhtml+xml"];

    public async Task<FetchedPage> FetchAsync(Uri url, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.1");
        request.Headers.AcceptLanguage.ParseAdd("en-GB,en;q=0.8");

        try
        {
            // Headers first: a PDF or a 50MB download is refused before its
            // body is read, not after.
            using var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            var finalUrl = response.RequestMessage?.RequestUri ?? url;

            if (!response.IsSuccessStatusCode)
            {
                throw new ContentUnavailableException(
                    $"GET {url} returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null && !HtmlMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
            {
                throw new ContentUnavailableException($"GET {url} returned {mediaType}, not a web page.");
            }

            if (response.Content.Headers.ContentLength > options.MaxResponseBytes)
            {
                throw new ContentUnavailableException(
                    $"GET {url} is {response.Content.Headers.ContentLength} bytes, over the {options.MaxResponseBytes} limit.");
            }

            // Also enforces the limit when the server didn't send a length.
            await response.Content.LoadIntoBufferAsync(options.MaxResponseBytes, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            return new FetchedPage(finalUrl, (int)response.StatusCode, html, FetchMethod.Http);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            // Not the caller, so it's HttpClient.Timeout.
            throw new ContentUnavailableException(
                $"GET {url} timed out after {options.TimeoutSeconds}s.", exception);
        }
        catch (HttpRequestException exception)
        {
            // DNS failure, refused connection, TLS error, too many
            // redirects, body over the limit, or PublicAddressGuard
            // refusing a private address (it's the inner exception).
            throw new ContentUnavailableException($"GET {url} failed: {exception.Message}", exception);
        }
    }
}
