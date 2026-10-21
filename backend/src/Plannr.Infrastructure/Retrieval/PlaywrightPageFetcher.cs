using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Plannr.Core.Capture;
using Plannr.Core.Exceptions;
using Plannr.Infrastructure.Options;

namespace Plannr.Infrastructure.Retrieval;

/// <summary>
/// Loads a page in headless Chromium so its JavaScript runs, then hands
/// back the resulting HTML. Only used when plain HTTP got a shell (see
/// WebPageRetriever); later reused for TikTok, where it's the main route.
///
/// One browser per process, started on first use and shared; each fetch
/// gets its own context (fresh cookies, storage, cache) that is thrown
/// away afterwards, so nothing from one user's page leaks into another's.
///
/// Every request the page makes goes through a filter:
///  - images, media and fonts are never downloaded (faster, and the spec's
///    "no images kept" point never comes up);
///  - every other request is checked against PublicAddressGuard, so a page
///    can't make the browser call our own network either.
/// The browser resolves DNS itself, so unlike HttpPageFetcher this check
/// can't pin the address it connects to; a fast DNS-rebinding attack could
/// slip between the two. In production the Worker's network rules close
/// that gap (Milestone 4/AWS).
/// </summary>
public sealed class PlaywrightPageFetcher : IBrowserPageFetcher, IAsyncDisposable, IDisposable
{
    private static readonly HashSet<string> SkippedResourceTypes = ["image", "media", "font"];

    private readonly RetrievalOptions _options;
    private readonly ILogger<PlaywrightPageFetcher> _logger;
    private readonly Func<Uri, CancellationToken, ValueTask<bool>> _isAllowed;
    private readonly SemaphoreSlim _startLock = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightPageFetcher(RetrievalOptions options, ILogger<PlaywrightPageFetcher> logger)
        : this(options, logger, IsPublicHostAsync)
    {
    }

    /// <summary>
    /// Lets the tests serve pages from a local test server, which the real
    /// filter would (correctly) refuse.
    /// </summary>
    internal PlaywrightPageFetcher(
        RetrievalOptions options,
        ILogger<PlaywrightPageFetcher> logger,
        Func<Uri, CancellationToken, ValueTask<bool>> isAllowed)
    {
        _options = options;
        _logger = logger;
        _isAllowed = isAllowed;
    }

    public async Task<FetchedPage> FetchAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var browser = await GetBrowserAsync(cancellationToken);

        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = _options.UserAgent,
            Locale = "en-GB",
            TimezoneId = "Europe/London",

            // A service worker could fetch on the page's behalf without
            // passing through the route filter below.
            ServiceWorkers = ServiceWorkerPolicy.Block
        });

        // Playwright's calls don't take a CancellationToken; closing the
        // context makes whatever is in flight fail, and the catch below
        // turns that back into the caller's cancellation.
        await using var onCancel = cancellationToken.Register(() => _ = context.CloseAsync());

        await context.RouteAsync("**/*", route => FilterAsync(route, cancellationToken));

        try
        {
            var page = await context.NewPageAsync();

            var response = await page.GotoAsync(url.ToString(), new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = _options.BrowserTimeoutSeconds * 1000f
            });

            await WaitForQuietAsync(page, url);

            var status = response?.Status ?? (int)HttpStatusCode.OK;
            if (status >= 400)
            {
                throw new ContentUnavailableException($"Browser GET {url} returned {status}.");
            }

            var html = await page.ContentAsync();
            return new FetchedPage(new Uri(page.Url), status, html, FetchMethod.Browser);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new ContentUnavailableException(
                $"Browser GET {url} timed out after {_options.BrowserTimeoutSeconds}s.", exception);
        }
        catch (PlaywrightException exception)
        {
            // net::ERR_NAME_NOT_RESOLVED, net::ERR_BLOCKED_BY_CLIENT (our
            // filter refused the page itself), a crashed tab...
            throw new ContentUnavailableException($"Browser GET {url} failed: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Waits for the page's scripts to stop making requests, up to
    /// BrowserSettleSeconds. Not reaching quiet isn't a failure -- some pages
    /// poll forever -- so a timeout here just means "read what's there".
    /// </summary>
    private async Task WaitForQuietAsync(IPage page, Uri url)
    {
        if (_options.BrowserSettleSeconds == 0)
        {
            return;
        }

        try
        {
            await page.WaitForLoadStateAsync(
                LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = _options.BrowserSettleSeconds * 1000f });
        }
        catch (TimeoutException)
        {
            _logger.LogDebug(
                "{Url} was still making requests after {Seconds}s; reading it as it is.",
                url, _options.BrowserSettleSeconds);
        }
    }

    private async Task FilterAsync(IRoute route, CancellationToken cancellationToken)
    {
        var request = route.Request;

        if (SkippedResourceTypes.Contains(request.ResourceType))
        {
            await route.AbortAsync();
            return;
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var target)
            || target.Scheme is not ("http" or "https")
            || !await _isAllowed(target, cancellationToken))
        {
            _logger.LogInformation("Browser request to {Target} refused: not a public http(s) address.", request.Url);
            await route.AbortAsync("blockedbyclient");
            return;
        }

        await route.ContinueAsync();
    }

    private static async ValueTask<bool> IsPublicHostAsync(Uri target, CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(target.DnsSafeHost, cancellationToken);
            return addresses.Length > 0 && addresses.All(PublicAddressGuard.IsPublic);
        }
        catch (System.Net.Sockets.SocketException)
        {
            // Doesn't resolve: let the browser fail it with its own
            // net::ERR_NAME_NOT_RESOLVED, which is the clearer message.
            return true;
        }
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsConnected: true })
        {
            return _browser;
        }

        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_browser is { IsConnected: true })
            {
                return _browser;
            }

            _playwright ??= await Playwright.CreateAsync();

            try
            {
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            }
            catch (PlaywrightException exception)
            {
                // Almost always "Executable doesn't exist": a setup problem,
                // not a bad page, so it says how to fix it.
                throw new InvalidOperationException(
                    "Chromium could not be started. Install it once with " +
                    "'powershell bin/Debug/net10.0/playwright.ps1 install chromium' from an executable project " +
                    "(e.g. src/Plannr.Eval), or set Retrieval:BrowserFallbackEnabled to false.",
                    exception);
            }

            _logger.LogInformation("Started headless Chromium {Version} for JavaScript pages.", _browser.Version);
            return _browser;
        }
        finally
        {
            _startLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        _startLock.Dispose();
    }

    /// <summary>
    /// For containers disposed synchronously: without this, disposing one
    /// that holds this singleton throws ("only implements IAsyncDisposable").
    /// Blocking is acceptable here -- it runs once, at shutdown.
    /// </summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
