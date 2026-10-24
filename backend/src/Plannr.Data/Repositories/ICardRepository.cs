using Plannr.Services.DTOs;

namespace Plannr.Data.Repositories;

/// <summary>
/// ActivityCards and their CardFieldFlags. Only what the result hand-off
/// needs so far; M3 adds the wishlist (list, get, edit, delete).
/// </summary>
public interface ICardRepository
{
    /// <summary>
    /// Saves the card a worker made, with its flags. In the EF Core version
    /// this shares one SaveChanges with the capture's CompleteAsync, so the
    /// card and the capture's "Done" are saved together or not at all.
    /// </summary>
    /// <returns>The new card's id.</returns>
    Task<long> AddFromDraftAsync(string userId, CardDraft draft, CancellationToken cancellationToken = default);
}
