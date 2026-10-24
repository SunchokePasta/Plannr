using Plannr.Services.DTOs;
using Plannr.Services.Exceptions;

namespace Plannr.Services.Interfaces;

/// <summary>
/// Fetches what can be read about one kind of link. One implementation per
/// <see cref="SourceType"/>; the pipeline picks the one whose
/// <see cref="Handles"/> matches, so supporting a new link type means
/// adding a class, not editing the pipeline.
/// </summary>
public interface IContentRetriever
{
    SourceType Handles { get; }

    /// <exception cref="Exceptions.ContentUnavailableException">
    /// The page couldn't be fetched: not found, blocked, timed out.
    /// </exception>
    Task<RetrievedContent> RetrieveAsync(Uri url, CancellationToken cancellationToken = default);
}
