using Plannr.Core.Options;

namespace Plannr.Infrastructure.Options;

/// <summary>
/// How shared pages are fetched and read, bound from the "Retrieval"
/// section. The text limits exist because everything kept here goes into
/// the Groq prompt, and Groq counts prompt tokens against the rate limit.
/// </summary>
public sealed class RetrievalOptions : AppOptionsBase
{
    public const string SectionName = "Retrieval";

    /// <summary>
    /// Says honestly what Plannr is and where to read about it, rather than
    /// posing as a browser (the spec's BCS and ethics commitments). Some
    /// sites will refuse it; the data pool measures how many.
    /// </summary>
    public string UserAgent { get; set; } = "PlannrBot/0.1 (+https://github.com/SunchokePasta/Plannr)";

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Enough for http -> https -> www -> a short link's target; more is usually a loop.</summary>
    public int MaxRedirects { get; set; } = 5;

    /// <summary>A page bigger than this isn't an event listing; it's a reason to stop reading.</summary>
    public long MaxResponseBytes { get; set; } = 2_000_000;

    /// <summary>The most of a page's readable text passed on; the rest is cut at a word boundary.</summary>
    public int MaxBodyTextChars { get; set; } = 6000;

    /// <summary>
    /// The most JSON-LD passed on. Ticket sites can embed huge objects
    /// (every price tier, every seat map), and the first event is nearly
    /// always the one that matters.
    /// </summary>
    public int MaxStructuredDataChars { get; set; } = 8000;

    /// <summary>
    /// Retry a page in headless Chromium when plain HTTP got a JavaScript
    /// shell. Off means HTTP only -- useful where Chromium isn't installed.
    /// </summary>
    public bool BrowserFallbackEnabled { get; set; } = true;

    /// <summary>
    /// Below this much readable text, with no JSON-LD either, a page is
    /// treated as one that only fills in with JavaScript.
    /// </summary>
    public int MinBodyTextChars { get; set; } = 200;

    /// <summary>The longest the browser may take to load the page itself.</summary>
    public int BrowserTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// After the page loads, how long to wait for its scripts' requests to
    /// go quiet. Some pages never go quiet (live chat, analytics), so this
    /// is a ceiling, not a requirement: the HTML is read either way.
    /// </summary>
    public int BrowserSettleSeconds { get; set; } = 5;

    public override void Validate()
    {
        if (string.IsNullOrWhiteSpace(UserAgent))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(UserAgent)} must be set.");
        }

        if (TimeoutSeconds is < 1 or > 120)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(TimeoutSeconds)} must be between 1 and 120, not {TimeoutSeconds}.");
        }

        if (MaxRedirects is < 0 or > 10)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MaxRedirects)} must be between 0 and 10, not {MaxRedirects}.");
        }

        if (MaxResponseBytes < 100_000)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MaxResponseBytes)} must be at least 100000, not {MaxResponseBytes}.");
        }

        if (MaxBodyTextChars < 500)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MaxBodyTextChars)} must be at least 500, not {MaxBodyTextChars}.");
        }

        if (MaxStructuredDataChars < 500)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MaxStructuredDataChars)} must be at least 500, not {MaxStructuredDataChars}.");
        }

        if (MinBodyTextChars < 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MinBodyTextChars)} can't be negative, not {MinBodyTextChars}.");
        }

        if (BrowserTimeoutSeconds is < 1 or > 120)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(BrowserTimeoutSeconds)} must be between 1 and 120, not {BrowserTimeoutSeconds}.");
        }

        if (BrowserSettleSeconds < 0 || BrowserSettleSeconds > BrowserTimeoutSeconds)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(BrowserSettleSeconds)} must be between 0 and " +
                $"{nameof(BrowserTimeoutSeconds)} ({BrowserTimeoutSeconds}), not {BrowserSettleSeconds}.");
        }
    }
}
