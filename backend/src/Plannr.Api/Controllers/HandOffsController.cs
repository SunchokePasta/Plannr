using Microsoft.AspNetCore.Mvc;
using Plannr.Api.HandOffs;
using Plannr.Api.Options;
using Plannr.Data.Entities;
using Plannr.Data.Repositories;
using Plannr.Services.DTOs;
using Plannr.Services.HandOffs;

namespace Plannr.Api.Controllers;

/// <summary>
/// The worker's way in (D37, D41): it claims jobs and hands back results
/// here instead of touching the database. Only the worker knows the shared
/// secret, and these endpoints are left out of the public API document.
/// </summary>
[ApiController]
[Route("internal")]
[RequireSharedSecret]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class HandOffsController(
    ICaptureRepository captures,
    ICardRepository cards,
    WakeSignal announcements,
    HandOffOptions options) : ControllerBase
{
    private readonly ICaptureRepository _captures = captures;
    private readonly ICardRepository _cards = cards;

    /// <summary>The announcer's doorbell (the API has one WakeSignal; the announcer waits on it).</summary>
    private readonly WakeSignal _announcements = announcements;

    private readonly HandOffOptions _options = options;

    /// <summary>Job hand-off: gives the worker the next capture to run, if there is one.</summary>
    /// <response code="200">A job, now leased to this worker.</response>
    /// <response code="204">Nothing waiting.</response>
    [HttpPost("jobs/claim")]
    public Task<ActionResult<ClaimedCapture>> ClaimAsync(ClaimRequest request, CancellationToken cancellationToken)
    {
        // TODO(H3.3): ask the repository for the next job, with this worker's id and the configured
        //   lease (_options.Lease) and attempt limit (_options.MaxAttempts). The one-step claim itself
        //   is the repository's job (ICaptureRepository.TryClaimNextAsync); this only passes the answer on:
        //   - null: return NoContent() (204: nothing waiting).
        //   - a capture: return a ClaimedCapture made from its Id, SourceUrl and Attempts.
        //     (ActionResult<T> converts from a plain T, so `return new ClaimedCapture(...)` works.)
        throw new NotImplementedException("TODO(H3.3): see docs/guides/hand-offs.md");
    }

    /// <summary>
    /// Result hand-off: saves the worker's card (or failure) and wakes the
    /// announcer. Safe to repeat: if the capture already finished, nothing
    /// changes and the answer is still 200, so a worker that didn't hear
    /// the first reply can simply send again.
    /// </summary>
    /// <response code="200">Saved, or already saved.</response>
    /// <response code="400">Not exactly one of a card or a failure.</response>
    /// <response code="404">No such capture.</response>
    [HttpPost("captures/{captureId:long}/result")]
    public Task<IActionResult> ReportResultAsync(long captureId, CaptureResult result, CancellationToken cancellationToken)
    {
        // TODO(H5.1): in order, returning as soon as one applies:
        //   1. Neither a card nor a failure, or both: BadRequest(). (Compare (result.Card is null) with
        //      (result.Failure is null): equal means neither or both.)
        //   2. _captures.FindAsync: null means NotFound().
        //   3. Already Done or Failed: Ok() and change nothing. This is what makes a repeat safe
        //      ("idempotent"): the worker may resend a result it never heard the answer to.
        //   4. A card: _cards.AddFromDraftAsync(capture.UserId, card) gives the new card's id; then
        //      _captures.CompleteAsync(captureId, cardId).
        //      A failure: _captures.FailAsync(captureId, its UserMessage, its ErrorReference).
        //   5. _announcements.Nudge(), so the user is told now rather than at the next sweep; then Ok().
        throw new NotImplementedException("TODO(H5.1): see docs/guides/hand-offs.md");
    }
}
