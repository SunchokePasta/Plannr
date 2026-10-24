using System.Net;
using Plannr.Services.APIs;
using Plannr.Services.Diagnostics;
using Plannr.Services.DTOs;
using Plannr.Services.Exceptions;
using Plannr.Services.Interfaces;
using Plannr.Worker.Options;
using Refit;

namespace Plannr.Worker.HandOffs;

/// <summary>
/// One job, start to finish: run the extraction pipeline, then hand the
/// card (or the failure) back to the API (D37). The worker's error
/// boundary: every failure becomes a result the user sees, with an error
/// reference that finds this worker's log line.
/// </summary>
public sealed class CaptureJobProcessor(
    IExtractionPipeline pipeline,
    IPlannrInternalApi api,
    WorkerOptions options,
    TimeProvider time,
    ILogger<CaptureJobProcessor> logger) : ICaptureJobProcessor
{
    /// <summary>What the user sees for a failure that isn't a CaptureException (a bug, not a bad link).</summary>
    public const string UnexpectedFailureMessage = "Something went wrong reading this. Try sharing it again.";

    private readonly IExtractionPipeline _pipeline = pipeline;
    private readonly IPlannrInternalApi _api = api;
    private readonly WorkerOptions _options = options;

    /// <summary>The clock retry waits are measured on (a fake one in tests).</summary>
    private readonly TimeProvider _time = time;

    private readonly ILogger<CaptureJobProcessor> _logger = logger;

    public async Task ProcessAsync(ClaimedCapture job, CancellationToken cancellationToken)
    {
        var result = await ExtractAsync(job, cancellationToken);
        await HandBackAsync(job.CaptureId, result, cancellationToken);
    }

    private Task<CaptureResult> ExtractAsync(ClaimedCapture job, CancellationToken cancellationToken)
    {
        // TODO(H4.6): the error boundary (make the method async). One try with four outcomes:
        //   - success: CaptureResult.Succeeded(await _pipeline.ExtractFromLinkAsync(job.SourceUrl, token)).
        //   - catch (OperationCanceledException) when the token is cancelled: `throw;`. Shutting down is
        //     not a failure: the lease runs out and another worker runs it again.
        //   - catch (CaptureException): an expected outcome for some links. Mint ErrorReference.New(),
        //     log a warning starting "[{Reference}]" with the exception, and return
        //     CaptureResult.Failed(exception.UserMessage, reference).
        //   - catch (Exception): a bug. The same, but log an error and use UnexpectedFailureMessage
        //     (never a bug's own message: it may name internals).
        //   The order matters: C# takes the first catch that fits, so the specific ones go first.
        throw new NotImplementedException("TODO(H4.6): see docs/guides/hand-offs.md");
    }

    /// <summary>
    /// Sends the result, retrying while the API is unreachable or erroring.
    /// Repeating is safe (the API ignores a second copy). If it never gets
    /// through, the lease runs out and the capture is run again: wasted
    /// work, but nothing lost.
    /// </summary>
    private Task HandBackAsync(long captureId, CaptureResult result, CancellationToken cancellationToken)
    {
        // TODO(H4.7): a retry loop (make the method async): for attempt = 1, 2, ...:
        //   - try: await _api.ReportResultAsync(captureId, result, token); return.
        //   - catch (Exception) when the token isn't cancelled:
        //       not IsWorthRetrying, or this was attempt _options.ReportAttempts: log an error
        //       (with ErrorReference.New()) saying the lease will run out and it will run again; return.
        //       Never throw from here.
        //       Otherwise log a warning and wait longer each time before the next attempt:
        //       await Task.Delay(TimeSpan.FromSeconds(_options.ReportRetryBaseSeconds * attempt), _time, token).
        throw new NotImplementedException("TODO(H4.7): see docs/guides/hand-offs.md");
    }

    /// <summary>
    /// Network failures, our own timeout and 5xx answers are usually
    /// momentary. A 4xx (wrong secret, unknown capture, bad shape) would
    /// fail the same way again.
    /// </summary>
    private static bool IsWorthRetrying(Exception exception)
    {
        // TODO(H4.7): a switch expression on the exception's type:
        //   HttpRequestException (couldn't connect) => true;
        //   OperationCanceledException (here: the client's own timeout) => true;
        //   ApiException (Refit: the API answered with an error) => its StatusCode >= HttpStatusCode.InternalServerError;
        //   anything else => false.
        throw new NotImplementedException("TODO(H4.7): see docs/guides/hand-offs.md");
    }
}
