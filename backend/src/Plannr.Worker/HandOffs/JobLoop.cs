using Plannr.Services.APIs;
using Plannr.Services.Diagnostics;
using Plannr.Services.DTOs;
using Plannr.Services.HandOffs;
using Plannr.Worker.Options;

namespace Plannr.Worker.HandOffs;

/// <summary>
/// The worker's main loop, its side of the job hand-off (D37): claim jobs
/// from the API while a slot is free, run each one in the background, and
/// otherwise wait for a nudge (the API's wake call, or one of its own jobs
/// finishing) or the sweep interval, whichever comes first.
///
/// A slot is a place for one job to run. There are WorkerOptions.Slots of
/// them, so that many captures run at once and no more.
///
/// A BackgroundService: the host calls ExecuteAsync once at startup, and
/// cancels its token at shutdown.
/// </summary>
public sealed class JobLoop(
    IServiceScopeFactory scopes,
    WakeSignal signal,
    WorkerOptions options,
    ILogger<JobLoop> logger) : BackgroundService
{
    /// <summary>
    /// Makes short-lived services (the API client, a job's processor) for
    /// this long-lived one: `using var scope = _scopes.CreateScope();` then
    /// `scope.ServiceProvider.GetRequiredService&lt;T&gt;()`.
    /// </summary>
    private readonly IServiceScopeFactory _scopes = scopes;

    private readonly WakeSignal _signal = signal;
    private readonly WorkerOptions _options = options;
    private readonly ILogger<JobLoop> _logger = logger;

    // TODO(H4.2): the slots, as a field: a SemaphoreSlim starting with _options.Slots free places
    //   (new SemaphoreSlim(initialCount, maxCount), both the slot count). WaitAsync takes a place
    //   (WaitAsync(0) only if one is free right now, answering true or false); Release gives one back.

    /// <summary>For the tests: slots not running a job right now.</summary>
    public int FreeSlots =>
        // TODO(H4.2): the semaphore's CurrentCount.
        throw new NotImplementedException("TODO(H4.2): see docs/guides/hand-offs.md");

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO(H4.3): the loop (make the method async). Until stoppingToken is cancelled:
        //   1. Run a round: ClaimWhileSlotsFreeAsync(stoppingToken). It can throw when the API is
        //      down: catch (Exception) when it isn't an OperationCanceledException, log a warning,
        //      and carry on. The loop must survive anything.
        //   2. await _signal.WaitAsync(_options.Sweep, stoppingToken): sleep until a nudge or the
        //      sweep. At shutdown this throws OperationCanceledException, which ends the method.
        //   Then, in a finally: wait for running jobs to finish. Taking every slot back
        //   (WaitAsync(CancellationToken.None), _options.Slots times) means none is running.
        throw new NotImplementedException("TODO(H4.3): see docs/guides/hand-offs.md");
    }

    /// <summary>
    /// One round: claims and starts jobs until every slot is busy or the API
    /// has nothing waiting. Public so the tests can run a round without the loop.
    /// </summary>
    public Task ClaimWhileSlotsFreeAsync(CancellationToken cancellationToken)
    {
        // TODO(H4.4): (make the method async) while a slot is free (await the semaphore's WaitAsync(0, token)):
        //   1. Claim: in a new scope, get IPlannrInternalApi and await
        //      ClaimAsync(new ClaimRequest(_options.WorkerId), token). If that throws, give the slot
        //      back first (Release), then let the exception go up to the loop (`throw;`).
        //   2. null means nothing waiting: give the slot back and stop the round.
        //   3. A job: log that you claimed it, and start it without waiting for it:
        //      _ = Task.Run(() => RunJobAsync(job, token), CancellationToken.None).
        //      The slot stays taken; RunJobAsync gives it back when the job ends.
        throw new NotImplementedException("TODO(H4.4): see docs/guides/hand-offs.md");
    }

    private Task RunJobAsync(ClaimedCapture job, CancellationToken cancellationToken)
    {
        // TODO(H4.5): run one job, and always give its slot back (make the method async):
        //   try: in a new scope, get ICaptureJobProcessor and await ProcessAsync(job, token).
        //   catch OperationCanceledException when the token is cancelled: shutting down, nothing to do
        //     (the lease runs out and the job runs again).
        //   catch (Exception): the processor shouldn't throw, but log an error with
        //     ErrorReference.New() anyway: the loop mustn't lose a slot to a bug.
        //   finally: Release the slot, then _signal.Nudge(): a slot just came free, so look for more
        //     work now rather than at the next sweep.
        throw new NotImplementedException("TODO(H4.5): see docs/guides/hand-offs.md");
    }
}
