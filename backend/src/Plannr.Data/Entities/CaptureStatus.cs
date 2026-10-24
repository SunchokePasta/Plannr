namespace Plannr.Data.Entities;

/// <summary>Where a capture is on its way to becoming a card. Only ever moves forward, except a lease running out (Processing back to claimable).</summary>
public enum CaptureStatus
{
    /// <summary>Saved by the API; no worker has it yet.</summary>
    Waiting,

    /// <summary>Claimed by a worker, which holds a lease on it (<see cref="Capture.ClaimedAt"/>).</summary>
    Processing,

    /// <summary>A card was made (<see cref="Capture.CardId"/>).</summary>
    Done,

    /// <summary>No card could be made (<see cref="Capture.FailureMessage"/>, <see cref="Capture.FailureReference"/>).</summary>
    Failed
}
