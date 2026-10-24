using Plannr.Services.DTOs;

namespace Plannr.Worker.HandOffs;

/// <summary>Runs one claimed capture and hands its result back. An interface so the job loop's tests can control how long a job takes.</summary>
public interface ICaptureJobProcessor
{
    /// <summary>Never throws, except OperationCanceledException when the worker is shutting down.</summary>
    Task ProcessAsync(ClaimedCapture job, CancellationToken cancellationToken);
}
