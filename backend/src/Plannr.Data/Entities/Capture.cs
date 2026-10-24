namespace Plannr.Data.Entities;

/// <summary>
/// One thing a user shared, and its progress towards a card. The row is the
/// truth for both hand-offs (D37): the API saves it, a worker claims it
/// through the API, the worker's result finishes it, and the API announces
/// it. Nothing else holds a capture's state, so a crash anywhere loses
/// nothing: the row says what's left to do.
///
/// Plain properties only; the rules for changing them are in
/// <see cref="Repositories.ICaptureRepository"/>, because the claim has to
/// happen in one SQL statement (two workers must never both win).
/// </summary>
public sealed class Capture
{
    public long Id { get; set; }

    /// <summary>The Identity user who shared it (Identity's ids are strings).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The link as shared. Screenshots join in M6 (an image instead of a link).</summary>
    public required Uri SourceUrl { get; set; }

    public CaptureStatus Status { get; set; } = CaptureStatus.Waiting;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The worker holding it while Processing (its <c>ClaimRequest.WorkerId</c>), for the logs.</summary>
    public string? ClaimedBy { get; set; }

    /// <summary>
    /// When the current lease started. A Processing capture whose lease is
    /// older than the configured length belongs to a worker that went quiet
    /// (crashed, restarted, lost its network), and is claimed again.
    /// </summary>
    public DateTimeOffset? ClaimedAt { get; set; }

    /// <summary>How many times it's been claimed. Past the limit, an expired lease fails it rather than retrying.</summary>
    public int Attempts { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>The card made from it, when Done.</summary>
    public long? CardId { get; set; }

    /// <summary>When Failed: safe to show the user as it is.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>When Failed: the error reference that finds the log line with the details.</summary>
    public string? FailureReference { get; set; }

    /// <summary>
    /// When the user was told it finished (SignalR or a push). Null on a
    /// finished capture means the announcement still has to happen: that's
    /// what the API's announcement sweep looks for.
    /// </summary>
    public DateTimeOffset? AnnouncedAt { get; set; }
}
