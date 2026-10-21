namespace Plannr.Infrastructure.Retrieval;

/// <summary>
/// A shared link pointed (directly, by DNS, or through a redirect) at an
/// address that isn't on the public internet. Thrown by
/// <see cref="PublicAddressGuard"/>; reaches callers wrapped in the
/// HttpRequestException, then the ContentUnavailableException, that every
/// other connection failure does.
/// </summary>
public sealed class BlockedAddressException(string message) : IOException(message);
