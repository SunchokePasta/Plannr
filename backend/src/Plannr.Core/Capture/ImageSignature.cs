namespace Plannr.Core.Capture;

/// <summary>
/// Recognises an image by its first bytes (its "magic number"), which a file
/// can't fake the way it can fake a name or a declared media type.
/// </summary>
public static class ImageSignature
{
    /// <returns>"image/png", "image/jpeg" or "image/webp", or null for anything else.</returns>
    public static string? Detect(ReadOnlySpan<byte> bytes)
    {
        // 89 50 4E 47 0D 0A 1A 0A
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        // FF D8 FF
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        // "RIFF" <4-byte size> "WEBP"
        if (bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
