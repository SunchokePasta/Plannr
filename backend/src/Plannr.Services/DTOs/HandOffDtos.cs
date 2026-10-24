namespace Plannr.Services.DTOs;

// What the API and the worker send each other in the two hand-offs (D37).
// Both programs use these exact types, so the shapes can't drift apart.

/// <summary>The worker asking the API for its next job (POST /internal/jobs/claim).</summary>
/// <param name="WorkerId">Who's claiming: stored on the capture, so the logs say which worker had it.</param>
public sealed record ClaimRequest(string WorkerId);

/// <summary>A job the API has handed to one worker: the capture is now Processing, leased to that worker.</summary>
/// <param name="Attempt">1 the first time; more when an earlier worker went quiet and the lease ran out.</param>
public sealed record ClaimedCapture(long CaptureId, Uri SourceUrl, int Attempt);

/// <summary>
/// The worker handing a finished job back (POST /internal/captures/{id}/result):
/// exactly one of <see cref="Card"/> or <see cref="Failure"/>.
/// </summary>
public sealed record CaptureResult
{
    public CardDraft? Card { get; init; }

    public CaptureFailure? Failure { get; init; }

    public static CaptureResult Succeeded(CardDraft card) => new() { Card = card };

    public static CaptureResult Failed(string userMessage, string errorReference) =>
        new() { Failure = new CaptureFailure(userMessage, errorReference) };
}

/// <param name="UserMessage">Safe to show on the phone as it is (a CaptureException's UserMessage).</param>
/// <param name="ErrorReference">Minted by the worker, so it points at the worker's log line.</param>
public sealed record CaptureFailure(string UserMessage, string ErrorReference);
