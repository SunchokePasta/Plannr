namespace Plannr.Core.Exceptions;

/// <summary>
/// An Instagram post or Reel. Reading them needs Meta's oEmbed approval (a
/// Could in the spec); until then the user is pointed at the screenshot
/// route, which is a Must and works today. When access is approved, this
/// class is deleted and an Instagram retriever added.
/// </summary>
public sealed class InstagramNotSupportedException(string message, Exception? innerException = null)
    : UnsupportedLinkException(
        message,
        "Instagram links aren't supported yet. Share a screenshot of the post instead.",
        innerException);
