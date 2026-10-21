namespace Plannr.Core.Capture;

/// <summary>
/// The card fields a flag can point at. An enum rather than a string so a
/// typo can't flag a field that doesn't exist; it is stored by name in
/// CardFieldFlags.FieldName.
/// </summary>
public enum CardField
{
    Title,
    Description,
    StartsAt,
    EndsAt,
    LocationName,
    Address,

    /// <summary>Covers PriceMinPence, PriceMaxPence and Currency together.</summary>
    Price,

    Category
}

public enum FlagReason
{
    /// <summary>Nothing was found for the field.</summary>
    Missing,

    /// <summary>A value was found, but the model's confidence was below the threshold.</summary>
    Uncertain,

    /// <summary>A value was found but couldn't be used (a date that won't parse, an end before the start).</summary>
    Invalid
}

/// <summary>
/// A field the user should check before relying on the card -- the
/// "uncertainty flags" Must. A flag is the answer to "we made a card but
/// aren't sure about this part"; failing to make a card at all is a
/// CaptureException instead.
/// </summary>
public sealed record FieldFlag(CardField Field, FlagReason Reason, double? Confidence = null);
