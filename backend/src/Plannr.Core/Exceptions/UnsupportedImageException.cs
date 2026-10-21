namespace Plannr.Core.Exceptions;

/// <summary>
/// A shared "screenshot" that isn't one Plannr can read: not a PNG, JPEG or
/// WebP by its actual bytes (whatever it claimed to be), empty, or too
/// large. Raised before anything is sent to Groq; the API answers 400.
/// </summary>
public sealed class UnsupportedImageException : CaptureException
{
    private UnsupportedImageException(string message, string userMessage)
        : base(message, userMessage, innerException: null)
    {
    }

    public static UnsupportedImageException NotAnImage(string declaredType, int bytes) =>
        new($"A {bytes}-byte upload declared as '{declaredType}' is not a PNG, JPEG or WebP image.",
            "That file isn't a screenshot Plannr can read. Share a PNG, JPEG or WebP image.");

    public static UnsupportedImageException TooLarge(int bytes, int maxBytes) =>
        new($"A {bytes}-byte image is over the {maxBytes}-byte limit.",
            $"That image is too large. Share a screenshot under {maxBytes / 1_000_000} MB.");
}
