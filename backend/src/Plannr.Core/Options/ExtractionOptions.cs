namespace Plannr.Core.Options;

/// <summary>
/// How the pipeline turns what it read into a card, bound from the
/// "Extraction" section. Lives in Core because the normaliser and pipeline
/// that use it do.
/// </summary>
public sealed class ExtractionOptions : AppOptionsBase
{
    public const string SectionName = "Extraction";

    /// <summary>
    /// An LLM field below this confidence is flagged Uncertain for the user to
    /// check. 0.6 sits between the prompt's "inferred" (0.5-0.8) and "guess"
    /// (below 0.5) bands, so guesses and weak inferences are flagged.
    /// </summary>
    public double FlagBelowConfidence { get; set; } = 0.6;

    public int MaxTitleChars { get; set; } = 120;

    public int MaxDescriptionChars { get; set; } = 500;

    /// <summary>A price above this (£2,000) is almost certainly a misread, not a ticket.</summary>
    public int MaxPricePence { get; set; } = 200_000;

    /// <summary>
    /// When the page's JSON-LD alone gives a title, a start with its time, a
    /// place and a price, don't call the LLM at all. Off makes every capture
    /// call it -- useful for comparing the two in Eval.
    /// </summary>
    public bool SkipLlmWhenStructuredComplete { get; set; } = true;

    /// <summary>A screenshot bigger than this (10 MB) is refused before it is sent anywhere.</summary>
    public int MaxImageBytes { get; set; } = 10_000_000;

    public override void Validate()
    {
        if (FlagBelowConfidence is < 0 or > 1)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(FlagBelowConfidence)} must be between 0 and 1, not {FlagBelowConfidence}.");
        }

        if (MaxTitleChars < 20 || MaxDescriptionChars < 50)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MaxTitleChars)} must be at least 20 and {nameof(MaxDescriptionChars)} at least 50.");
        }

        if (MaxPricePence < 100)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MaxPricePence)} must be at least 100, not {MaxPricePence}.");
        }

        if (MaxImageBytes < 100_000)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MaxImageBytes)} must be at least 100000, not {MaxImageBytes}.");
        }
    }
}
