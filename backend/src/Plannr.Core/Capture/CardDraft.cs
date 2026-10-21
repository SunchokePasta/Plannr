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
    public required Uri SourceUrl { get; init; }

    public required SourceType SourceType { get; init; }

    /// <summary>Carried from <see cref="RetrievedContent.HasEventData"/> so Eval can score events and web pages separately.</summary>
    public bool HasEventData { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    /// <summary>With its UK offset (GMT or BST), so it means the same moment wherever it's read.</summary>
    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public string? LocationName { get; init; }

    public string? Address { get; init; }

    /// <summary>
    /// Whole pence, never a decimal of pounds: integers can't pick up
    /// rounding errors. 0 means free; null means unknown.
    /// </summary>
    public int? PriceMinPence { get; init; }

    public int? PriceMaxPence { get; init; }

    /// <summary>ISO 4217, e.g. "GBP". Null when there's no price.</summary>
    public string? Currency { get; init; }

    public string? Category { get; init; }

    public Uri? ThumbnailUrl { get; init; }

    public IReadOnlyList<FieldFlag> Flags { get; init; } = [];
}
