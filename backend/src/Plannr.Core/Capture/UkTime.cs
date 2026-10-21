namespace Plannr.Core.Capture;

/// <summary>
/// Plannr's users and events are in the UK, so a time written without a
/// zone ("Sat 14 Dec, 7pm", or JSON-LD's "2026-12-14T19:00") means London
/// time. This turns such times into DateTimeOffsets with the right GMT/BST
/// offset, including on the two nights a year the clocks change.
/// </summary>
public static class UkTime
{
    /// <summary>The IANA id; .NET resolves it on Windows too (via ICU).</summary>
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static DateTimeOffset Now(TimeProvider time) => TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone);

    /// <summary>
    /// A UK wall-clock time as an exact moment.
    /// - Spring forward (01:00-02:00 doesn't exist): moved an hour later,
    ///   which is what a clock on the wall would show.
    /// - Fall back (01:00-02:00 happens twice): the first one, in BST --
    ///   an event at 01:30 that night almost certainly means before the
    ///   clocks went back.
    /// </summary>
    public static DateTimeOffset FromLocal(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        if (Zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        var offset = Zone.IsAmbiguousTime(local)
            ? Zone.GetAmbiguousTimeOffsets(local).Max()
            : Zone.GetUtcOffset(local);

        return new DateTimeOffset(local, offset);
    }
}
