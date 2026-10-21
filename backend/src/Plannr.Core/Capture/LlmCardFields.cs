namespace Plannr.Core.Capture;

/// <summary>
/// One field as the model gave it: its text, untouched, and how sure the
/// model says it is (0 to 1). Text rather than typed values because the
/// model's formatting isn't trusted -- "Sat 14th Dec, 7pm" becomes a date in
/// the normaliser, not here.
/// </summary>
public sealed record LlmField(string? Value, double Confidence);

/// <summary>
/// The LLM's answer for one shared item, before validation. A null field
/// means the model didn't return it at all; the normaliser flags it as
/// Missing either way.
/// </summary>
public sealed record LlmCardFields
{
    public LlmField? Title { get; init; }

    public LlmField? Description { get; init; }

    public LlmField? StartsAt { get; init; }

    public LlmField? EndsAt { get; init; }

    public LlmField? LocationName { get; init; }

    public LlmField? Address { get; init; }

    /// <summary>Free text such as "£5", "£5-£10" or "Free"; split into pence by the normaliser.</summary>
    public LlmField? Price { get; init; }

    public LlmField? Category { get; init; }
}
