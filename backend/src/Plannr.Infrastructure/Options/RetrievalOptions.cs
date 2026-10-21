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

    /// <summary>The most of a page's readable text passed on; the rest is cut at a word boundary.</summary>
    public int MaxBodyTextChars { get; set; } = 6000;

    /// <summary>
    /// The most JSON-LD passed on. Ticket sites can embed huge objects
    /// (every price tier, every seat map), and the first event is nearly
    /// always the one that matters.
    /// </summary>
    public int MaxStructuredDataChars { get; set; } = 8000;

    public override void Validate()
    {
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
    }
}
