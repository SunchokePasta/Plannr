namespace Plannr.Api.HandOffs;

/// <summary>
/// Rings the worker's doorbell after a new capture is saved, so it starts
/// within milliseconds instead of at its next sweep. Fire and forget.
/// </summary>
public interface IWorkerNudger
{
    /// <summary>Returns at once and never throws, whether or not the worker is reachable.</summary>
    void Nudge();
}
