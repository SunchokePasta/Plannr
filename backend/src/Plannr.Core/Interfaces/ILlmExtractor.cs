using Plannr.Core.Capture;

namespace Plannr.Core.Interfaces;

/// <summary>
/// Turns retrieved content (text, or an image) into the model's raw answer.
/// Hides which LLM it is: Groq now, and a paid API could replace it (one of
/// the spec's risk mitigations) without the pipeline changing.
/// </summary>
public interface ILlmExtractor
{
    /// <exception cref="Exceptions.ExtractionFailedException">
    /// The model couldn't be reached, or answered with something unusable, after retries.
    /// </exception>
    Task<LlmCardFields> ExtractAsync(RetrievedContent content, CancellationToken cancellationToken = default);
}
