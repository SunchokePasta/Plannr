using System.Threading.Channels;

namespace Plannr.Services.HandOffs;

/// <summary>
/// The "nudge" half of nudge and sweep (D37): a doorbell one part of a
/// program rings to wake a loop that's waiting for work. The worker's job
/// loop waits on one (rung by the API's wake call, and by its own jobs as
/// they finish); the API's result announcer waits on another (rung by the
/// result endpoint).
///
/// Ringing it many times before the loop wakes counts as once: the loop
/// does everything that's waiting each time it wakes, so one wake is
/// enough. If nobody rings it, the wait ends anyway after the sweep
/// interval, which is the "sweep": the loop checks for work just in case a
/// nudge was lost.
/// </summary>
public sealed class WakeSignal(TimeProvider time)
{
    /// <summary>The clock the sweep interval is measured on (a fake one in tests).</summary>
    private readonly TimeProvider _time = time;

    // TODO(H1.1): the doorbell itself, as a field: a Channel<bool> made with
    //   Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite }).
    //   Room for one item, and a write into a full channel is dropped: that's
    //   what makes "rung three times" count as once.

    /// <summary>Wakes the loop if it's waiting, or makes its next wait return at once. Never blocks.</summary>
    public void Nudge()
    {
        // TODO(H1.2): put one item in the channel without waiting (its Writer's TryWrite).
        throw new NotImplementedException("TODO(H1.2): see docs/guides/hand-offs.md");
    }

    /// <summary>
    /// Waits until a nudge or until <paramref name="sweepInterval"/> passes, whichever comes first.
    /// </summary>
    /// <returns>True if a nudge woke it, false if it was the sweep interval.</returns>
    public Task<bool> WaitAsync(TimeSpan sweepInterval, CancellationToken cancellationToken)
    {
        // TODO(H1.3): race "a nudge arrives" against "the sweep interval passes" (make the method async):
        //   1. A nudge already waiting (the Reader's TryRead)? Return true at once.
        //   2. Start both waits: Reader.WaitToReadAsync(token).AsTask(), and
        //      Task.Delay(sweepInterval, _time, token). Start the delay before awaiting anything.
        //   3. await Task.WhenAny(nudged, swept) hands back whichever finished first.
        //   4. Cancel the other, so it doesn't linger: give both a token from
        //      CancellationTokenSource.CreateLinkedTokenSource(cancellationToken), and cancel that.
        //   5. await the winner: if the caller cancelled, that rethrows, which is what the caller wants.
        //   6. The nudge won: take the item out (TryRead) and return true. The delay won: false.
        throw new NotImplementedException("TODO(H1.3): see docs/guides/hand-offs.md");
    }
}
