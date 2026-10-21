using Refit;

namespace Plannr.Infrastructure.Llm;

/// <summary>
/// Groq's OpenAI-compatible endpoints, through Refit as in Applr. The key is
/// set as a bearer header in AddPlannrLlm.
///
/// Refit rather than the OpenAI .NET SDK (the spec's open question): Applr
/// already proves this works against Groq, and Refit's ApiException exposes
/// the Groq-specific details the extractor needs -- the status code, the
/// json_validate_failed body, and the Retry-After header on a 429.
/// </summary>
public interface IGroqApi
{
    [Post("/chat/completions")]
    Task<GroqChatResponse> CreateChatCompletionAsync(
        [Body] GroqChatRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>What this key can use. Backs <c>Plannr.Eval models</c>.</summary>
    [Get("/models")]
    Task<GroqModelList> ListModelsAsync(CancellationToken cancellationToken = default);
}
