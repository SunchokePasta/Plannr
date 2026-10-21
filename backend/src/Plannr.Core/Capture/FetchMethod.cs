namespace Plannr.Core.Capture;

/// <summary>
/// How a page's HTML was obtained. Recorded on every capture so Eval can
/// report how many pages needed a real browser -- the evidence for (or
/// against) the HTTP-first design.
/// </summary>
public enum FetchMethod
{
    /// <summary>A plain HTTP GET: fast, and enough for any page that is rendered on the server.</summary>
    Http,

    /// <summary>Playwright's headless Chromium, for pages that only fill in with JavaScript.</summary>
    Browser
}
