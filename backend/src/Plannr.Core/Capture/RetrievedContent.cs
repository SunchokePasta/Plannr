namespace Plannr.Core.Capture;

/// <summary>
/// What a retriever hands to the LLM: everything we could read about the
/// shared item, before anyone has interpreted it. Every field but the
/// source is optional, because each source type fills in different ones.
///
/// Holds no creator details (usernames, bios), as the spec's legal section
/// requires, and is never stored -- it lives for one pipeline run.
/// </summary>
public sealed record RetrievedContent
{
    /// <summary>Where the content came from, after following redirects (TikTok short links).</summary>
    public required Uri SourceUrl { get; init; }

    public required SourceType SourceType { get; init; }

    /// <summary>The page's own title, or its Open Graph og:title.</summary>
    public string? Title { get; init; }

    /// <summary>og:description, or a video's caption / description.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The page's schema.org Event as raw JSON-LD, passed to the LLM as-is.
    /// Raw rather than parsed, because sites fill it in inconsistently and
    /// the model is better at reading a messy object than we are at
    /// anticipating every shape.
    /// </summary>
    public string? StructuredData { get; init; }

    /// <summary>
    /// True when the page embeds a schema.org Event. This is what splits
    /// "event pages" from "general web pages" for G3's per-type targets.
    /// </summary>
    public bool HasEventData { get; init; }

    /// <summary>The page's main readable text, trimmed to what the LLM's prompt allows.</summary>
    public string? BodyText { get; init; }

    /// <summary>og:image or a video thumbnail: a link only, the image itself is never fetched or kept.</summary>
    public Uri? ThumbnailUrl { get; init; }

    /// <summary>A shared screenshot's bytes. Held in memory for this run only, then discarded.</summary>
    public byte[]? ImageBytes { get; init; }

    /// <summary>"image/png", "image/jpeg"... Set whenever <see cref="ImageBytes"/> is.</summary>
    public string? ImageMediaType { get; init; }
}
