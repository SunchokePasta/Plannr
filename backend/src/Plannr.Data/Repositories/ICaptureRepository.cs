using Plannr.Data.Entities;

namespace Plannr.Data.Repositories;

/// <summary>
/// The Captures table, and the rules for moving a capture between states
/// (D37). The API is its only user (D41): the worker reaches it through the
/// API's /internal endpoints.
///
/// The real version (CaptureRepository, on EF Core and MySQL) comes with
/// the database in M3; until then the tests use a fake.
/// </summary>
public interface ICaptureRepository
{
    /// <summary>Saves a newly shared link as <see cref="CaptureStatus.Waiting"/>.</summary>
    Task<Capture> AddAsync(string userId, Uri sourceUrl, CancellationToken cancellationToken = default);

    Task<Capture?> FindAsync(long captureId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the oldest capture a worker may start, all in one atomic step, so
    /// two workers asking at the same moment never get the same one:
    /// <list type="bullet">
    /// <item>a <see cref="CaptureStatus.Waiting"/> capture, or</item>
    /// <item>a <see cref="CaptureStatus.Processing"/> one whose lease (<see cref="Capture.ClaimedAt"/>)
    /// is older than <paramref name="lease"/>: its worker went quiet.</item>
    /// </list>
    /// The capture becomes Processing, leased to <paramref name="workerId"/>
    /// from now, with one more attempt. An expired one that has already had
    /// <paramref name="maxAttempts"/> is marked Failed instead of being handed out again.
    /// </summary>
    /// <returns>The claimed capture, or null when there's nothing to do.</returns>
    Task<Capture?> TryClaimNextAsync(string workerId, TimeSpan lease, int maxAttempts, CancellationToken cancellationToken = default);

    /// <summary>Marks it Done with its card. The first result wins: false (and no change) if it had already finished.</summary>
    Task<bool> CompleteAsync(long captureId, long cardId, CancellationToken cancellationToken = default);

    /// <summary>Marks it Failed. The first result wins: false (and no change) if it had already finished.</summary>
    Task<bool> FailAsync(long captureId, string userMessage, string errorReference, CancellationToken cancellationToken = default);

    /// <summary>Done or Failed captures whose user hasn't been told yet (<see cref="Capture.AnnouncedAt"/> is null), oldest first.</summary>
    Task<IReadOnlyList<Capture>> FindUnannouncedAsync(int max, CancellationToken cancellationToken = default);

    Task MarkAnnouncedAsync(long captureId, CancellationToken cancellationToken = default);
}
