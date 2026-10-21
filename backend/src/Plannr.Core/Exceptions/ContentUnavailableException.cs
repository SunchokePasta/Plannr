namespace Plannr.Core.Exceptions;

/// <summary>The page couldn't be fetched: not found, blocked, timed out.</summary>
public sealed class ContentUnavailableException(string message, Exception? innerException = null)
    : CaptureException(message, "We couldn't open that link. You can fill the card in yourself.", innerException);
