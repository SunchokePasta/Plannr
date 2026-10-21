using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Plannr.Core.Diagnostics;
using Plannr.Core.Exceptions;
using Plannr.Core.Interfaces;
using Plannr.Core.Options;

namespace Plannr.Core.Capture;

/// <summary>
/// The whole capture, in order:
///   1. classify the link (URL only);
///   2. retrieve its content with the retriever for that type;
///   3. map any JSON-LD by code;
///   4. ask the LLM -- unless the JSON-LD already gave everything;
///   5. normalise into a CardDraft with flags.
/// The Worker and Eval both call this, so the accuracy Eval measures is
/// the accuracy users get.
///
/// Failures are not logged here: they propagate as CaptureExceptions and
/// are logged once, with a reference, by the caller's boundary. The one
/// exception is an LLM failure when the page itself gave something usable:
/// that is caught, because it changes the outcome -- a partial card with
/// flags beats a failed capture for a user who is about to fill it in anyway.
/// </summary>
public sealed class ExtractionPipeline(
    ILinkClassifier classifier,
    IEnumerable<IContentRetriever> retrievers,
    ILlmExtractor llm,
    CardNormaliser normaliser,
    ExtractionOptions options,
    TimeProvider time,
    ILogger<ExtractionPipeline> logger) : IExtractionPipeline
{
    public async Task<CardDraft> ExtractFromLinkAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var sourceType = classifier.Classify(url);
        var retriever = retrievers.FirstOrDefault(r => r.Handles == sourceType)
            ?? throw new UnsupportedLinkException($"No retriever handles {sourceType} links yet ('{url}').");

        var content = await retriever.RetrieveAsync(url, cancellationToken);

        return await ExtractAsync(content, stopwatch, cancellationToken);
    }

    public async Task<CardDraft> ExtractFromImageAsync(byte[] image, string declaredMediaType, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (image.Length > options.MaxImageBytes)
        {
            throw UnsupportedImageException.TooLarge(image.Length, options.MaxImageBytes);
        }

        // Trust the bytes, not the label: the phone (or anyone calling the
        // API) can claim any media type.
        var mediaType = ImageSignature.Detect(image)
            ?? throw UnsupportedImageException.NotAnImage(declaredMediaType, image.Length);

        var content = new RetrievedContent
        {
            SourceUrl = null,
            SourceType = SourceType.Image,
            ImageBytes = image,
            ImageMediaType = mediaType
        };

        return await ExtractAsync(content, stopwatch, cancellationToken);
    }

    private async Task<CardDraft> ExtractAsync(RetrievedContent content, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var structured = JsonLdEventMapper.Map(content.StructuredData, UkTime.Now(time));

        LlmCardFields? answer = null;
        if (!(options.SkipLlmWhenStructuredComplete && structured?.IsComplete == true))
        {
            answer = await AskLlmAsync(content, structured, cancellationToken);
        }

        var card = normaliser.Normalise(content, structured, answer);

        logger.LogInformation(
            "Extracted {SourceType} {Source} in {ElapsedMs} ms (via {FetchedWith}, event data {HasEventData}, LLM {UsedLlm}): " +
            "{FlagCount} flag(s): {Flags}.",
            card.SourceType,
            card.SourceUrl?.ToString() ?? "screenshot",
            stopwatch.ElapsedMilliseconds,
            card.FetchedWith?.ToString() ?? "upload",
            card.HasEventData,
            card.UsedLlm,
            card.Flags.Count,
            card.Flags.Count == 0 ? "none" : string.Join(", ", card.Flags.Select(f => $"{f.Field}:{f.Reason}")));

        return card;
    }

    private async Task<LlmCardFields?> AskLlmAsync(RetrievedContent content, StructuredFields? structured, CancellationToken cancellationToken)
    {
        try
        {
            return await llm.ExtractAsync(content, cancellationToken);
        }
        catch (ExtractionFailedException exception) when (structured is not null || content.Title is not null)
        {
            var reference = ErrorReference.New();
            logger.LogWarning(
                exception,
                "[{Reference}] The LLM failed for {Source}; making the card from the page's own data only.",
                reference,
                content.SourceUrl);

            return null;
        }
    }
}
