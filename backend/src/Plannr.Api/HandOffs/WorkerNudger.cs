using Plannr.Services.APIs;

namespace Plannr.Api.HandOffs;

/// <summary>
/// Sends the worker its wake call in the background. A failed nudge is a
/// warning, not an error: the capture is safe in the database, and the
/// worker's sweep picks it up within a minute.
/// </summary>
public sealed class WorkerNudger(IWorkerWakeApi worker, ILogger<WorkerNudger> logger) : IWorkerNudger
{
    private readonly IWorkerWakeApi _worker = worker;
    private readonly ILogger<WorkerNudger> _logger = logger;

    public void Nudge()
    {
        // TODO(H3.2): "fire and forget": start the call and return without waiting for it.
        //   - Write a private async Task SendAsync() that awaits _worker.WakeAsync() inside a try/catch.
        //     The client's own timeout (HandOffs:NudgeTimeoutSeconds) stops it hanging.
        //   - catch (Exception): log a warning with the exception's Message (the worker being down is
        //     worth knowing about, but the capture is safe) and nothing more. Nothing may escape:
        //     nobody is awaiting this task, so an exception would go unseen.
        //   - Here: _ = Task.Run(SendAsync). Task.Run makes sure even the part of the call before its
        //     first await happens on another thread, so the phone's request never waits for it.
        throw new NotImplementedException("TODO(H3.2): see docs/guides/hand-offs.md");
    }
}
