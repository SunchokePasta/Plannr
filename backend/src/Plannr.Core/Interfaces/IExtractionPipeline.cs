using Plannr.Core.Capture;

namespace Plannr.Core.Interfaces;

/// <summary>
/// Shared link or screenshot in, card draft out. The one entry point the
/// Worker, Eval and (for its tests) the API use, so all of them run exactly
/// the same extraction.
/// </summary>
public interface IExtractionPipeline
{
    /// <exception cref="Exceptions.UnsupportedLinkException">Not a link Plannr can read (yet).</exception>
    /// <exception cref="Exceptions.ContentUnavailableException">The page couldn't be fetched.</exception>
    /// <exception cref="Exceptions.ExtractionFailedException">The LLM failed and the page gave nothing usable either.</exception>
    Task<CardDraft> ExtractFromLinkAsync(Uri url, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.UnsupportedImageException">Not a PNG/JPEG/WebP, empty, or too large.</exception>
    /// <exception cref="Exceptions.ExtractionFailedException">The vision model failed.</exception>
    Task<CardDraft> ExtractFromImageAsync(byte[] image, string declaredMediaType, CancellationToken cancellationToken = default);
}
