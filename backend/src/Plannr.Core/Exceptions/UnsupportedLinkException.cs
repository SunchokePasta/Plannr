namespace Plannr.Core.Exceptions;

/// <summary>
/// Not an http(s) link, or a kind Plannr doesn't read yet (Instagram is a
/// Could). The only CaptureException that can happen during a request:
/// POST /captures classifies the link before queuing it, and the API
/// answers 400.
/// </summary>
public sealed class UnsupportedLinkException(string message, Exception? innerException = null)
    : CaptureException(message, "Plannr can't read that kind of link yet.", innerException);
