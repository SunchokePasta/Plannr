using Refit;

namespace Plannr.Services.APIs;

/// <summary>
/// The worker's one private endpoint, as the API calls it: "there's a new
/// capture, check now rather than at your next sweep". It carries no data;
/// the worker claims the job through <see cref="IPlannrInternalApi"/>. If
/// the call is lost, nothing breaks: the worker's sweep finds the capture
/// within a minute.
/// </summary>
public interface IWorkerWakeApi
{
    [Post("/internal/wake")]
    Task WakeAsync(CancellationToken cancellationToken = default);
}
