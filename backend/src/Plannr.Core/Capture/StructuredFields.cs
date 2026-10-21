namespace Plannr.Core.Capture;

/// <summary>
/// What <see cref="JsonLdEventMapper"/> could read from a page's JSON-LD
/// by itself, already typed. Each field is null when the data didn't give
/// it in a form code can trust; the LLM is then asked instead.
/// </summary>
public sealed record StructuredFields
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public bool StartTimeKnown { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public bool EndTimeKnown { get; init; }

    public string? LocationName { get; init; }

    public string? Address { get; init; }

    public int? PriceMinPence { get; init; }

    public int? PriceMaxPence { get; init; }

    public string? Currency { get; init; }

    /// <summary>
    /// Every field the card needs is here with nothing left to guess: a
    /// title, a start with its time, a place and a price. When true, the
    /// pipeline can skip the LLM call entirely.
    /// </summary>
    public bool IsComplete =>
        Title is not null
        && StartsAt is not null && StartTimeKnown
        && LocationName is not null
        && PriceMinPence is not null;
}
