namespace Plannr.Core.Exceptions;

/// <summary>
/// Not an http(s) link, or a kind Plannr doesn't read yet. The only
/// CaptureException that can happen during a request: POST /captures
/// classifies the link before queuing it, and the API answers 400.
///
/// Not sealed: a link type with a better suggestion than "can't read
/// that" gets a subclass with its own UserMessage (see
/// <see cref="InstagramNotSupportedException"/>), and everything that
/// handles this type handles the subclass too.
/// </summary>
public class UnsupportedLinkException : CaptureException
{
    public const string DefaultUserMessage = "Plannr can't read that kind of link yet.";

    public UnsupportedLinkException(string message, Exception? innerException = null)
        : base(message, DefaultUserMessage, innerException)
    {
    }

    protected UnsupportedLinkException(string message, string userMessage, Exception? innerException)
        : base(message, userMessage, innerException)
    {
    }
}
