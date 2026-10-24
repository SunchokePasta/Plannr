using Plannr.Api.Notifications;
using Plannr.Api.Options;
using Plannr.Data.Repositories;
using Plannr.Services.Diagnostics;
using Plannr.Services.HandOffs;

namespace Plannr.Api.HandOffs;

/// <summary>
/// The end of the result hand-off (D37): tells each user their capture has
/// finished. Woken at once by the result endpoint (a nudge on its
/// WakeSignal), and every AnnouncementSweepSeconds anyway (the sweep), so
/// an announcement that failed, or was due when the API restarted, still
/// goes out.
///
/// The same shape as the worker's JobLoop (H4.3), without the slots.
/// </summary>
public sealed class ResultAnnouncer(
    IServiceScopeFactory scopes,
    WakeSignal signal,
    HandOffOptions options,
    ILogger<ResultAnnouncer> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopes = scopes;
    private readonly WakeSignal _signal = signal;
    private readonly HandOffOptions _options = options;
    private readonly ILogger<ResultAnnouncer> _logger = logger;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO(H5.2): the loop (make the method async). Until stoppingToken is cancelled:
        //   1. await AnnouncePendingAsync(stoppingToken), inside a try/catch that logs an error with
        //      ErrorReference.New() for anything but OperationCanceledException (a database blip mustn't
        //      end the loop: the next sweep tries again).
        //   2. await _signal.WaitAsync(_options.AnnouncementSweep, stoppingToken).
        //   Running a round before the first wait means anything left over from before a restart goes
        //   out at once.
        throw new NotImplementedException("TODO(H5.2): see docs/guides/hand-offs.md");
    }

    /// <summary>One round: announces every finished, unannounced capture (up to the batch size).</summary>
    /// <returns>How many were announced.</returns>
    internal Task<int> AnnouncePendingAsync(CancellationToken cancellationToken)
    {
        // TODO(H5.3): (make the method async)
        //   1. using var scope = _scopes.CreateScope(), and get ICaptureRepository and INotificationService
        //      from scope.ServiceProvider. The repository is scoped (one database context per piece of
        //      work); this service lives as long as the program, so it makes its own scope.
        //   2. For each capture from FindUnannouncedAsync(_options.AnnouncementBatchSize):
        //      CaptureFinishedAsync(capture), then MarkAnnouncedAsync(capture.Id), and count it.
        //      Tell first, then mark: a crash in between means telling twice, which beats never.
        //   3. Each capture in its own try/catch (not OperationCanceledException): log a warning with
        //      ErrorReference.New() and the capture's id, and go on to the next. One user's failure
        //      mustn't hold up everyone else's; the failed one stays unannounced for the next round.
        //   Return the count.
        throw new NotImplementedException("TODO(H5.3): see docs/guides/hand-offs.md");
    }
}
