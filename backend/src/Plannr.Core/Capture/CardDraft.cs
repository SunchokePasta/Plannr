namespace Plannr.Core.Capture;

/// <summary>
/// What the extraction pipeline produces: a validated, normalised card
/// ready for the user to check, with flags on anything doubtful.
///
/// Not the database entity. The ActivityCard entity (Milestone 3) is
/// created from one of these, and has things a draft doesn't -- an owner,
/// an id, timestamps.
/// </summary>
public sealed record CardDraft
{
    /// <summary>Null for a screenshot, which has no address.</summary>
    public required Uri? SourceUrl { get; init; }

    public required SourceType SourceType { get; init; }

    /// <summary>Carried from <see cref="RetrievedContent.HasEventData"/> so Eval can score events and web pages separately.</summary>
    public bool HasEventData { get; init; }

    /// <summary>Carried from <see cref="RetrievedContent.FetchedWith"/> so Eval can count browser fallbacks.</summary>
    public FetchMethod? FetchedWith { get; init; }

    /// <summary>False when the page's own data was complete enough that the LLM wasn't asked.</summary>
    public bool UsedLlm { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    /// <summary>With its UK offset (GMT or BST), so it means the same moment wherever it's read.</summary>
    public DateTimeOffset? StartsAt { get; init; }

    /// <summary>
    /// False when only the day is known ("Sat 14 Dec"): StartsAt is then
    /// midnight, and the app should show the date without a time.
    /// </summary>
    public bool StartTimeKnown { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public bool EndTimeKnown { get; init; }

    public string? LocationName { get; init; }

    public string? Address { get; init; }

    /// <summary>
    /// Whole pence, never a decimal of pounds: integers can't pick up
    /// rounding errors. 0 means free; null means unknown.
    /// </summary>
    public int? PriceMinPence { get; init; }

    /// <summary>Null with a PriceMinPence means "from £x" with no upper price given.</summary>
    public int? PriceMaxPence { get; init; }

    /// <summary>ISO 4217, e.g. "GBP". Null when there's no price.</summary>
    public string? Currency { get; init; }

    /// <summary>One of <see cref="ActivityCategories.All"/>, or null.</summary>
    public string? Category { get; init; }

    public Uri? ThumbnailUrl { get; init; }

    public IReadOnlyList<FieldFlag> Flags { get; init; } = [];

    /// <summary>Where each filled-in field came from; fields left empty aren't listed.</summary>
    public IReadOnlyDictionary<CardField, FieldSource> FieldSources { get; init; } = new Dictionary<CardField, FieldSource>();
}
