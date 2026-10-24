using Plannr.Services.DTOs;
using Refit;

namespace Plannr.Services.APIs;

/// <summary>
/// The API's private endpoints, as the worker calls them. The worker never
/// touches the database (D41): it claims jobs and hands back results here.
/// Each call takes milliseconds; the slow work happens between them, inside
/// the worker. Every request carries <see cref="HandOffs.SharedSecret"/>,
/// added once where the client is registered.
/// </summary>
public interface IPlannrInternalApi
{
    /// <returns>The claimed job, or null (204 No Content) when nothing is waiting.</returns>
    [Post("/internal/jobs/claim")]
    Task<ClaimedCapture?> ClaimAsync([Body] ClaimRequest request, CancellationToken cancellationToken = default);

    /// <summary>Safe to send more than once: the API saves the first copy and ignores the rest.</summary>
    [Post("/internal/captures/{captureId}/result")]
    Task ReportResultAsync(long captureId, [Body] CaptureResult result, CancellationToken cancellationToken = default);
}
