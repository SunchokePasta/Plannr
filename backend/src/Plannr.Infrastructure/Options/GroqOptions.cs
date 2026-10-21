using Plannr.Core.Options;

namespace Plannr.Infrastructure.Options;

/// <summary>
/// The Groq LLM, bound from the "Groq" section. Only programs that call
/// Groq (Eval, later the Worker) bind it, and they refuse to start without
/// a key -- the API never touches Groq, so it never needs one.
///
/// ApiKey never goes in a committed file: set it with
/// <c>dotnet user-secrets set "Groq:ApiKey" "..."</c> locally and SSM
/// Parameter Store in AWS.
/// </summary>
public sealed class GroqOptions : AppOptionsBase
{
    public const string SectionName = "Groq";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    /// <summary>
    /// Applr's working model at the time of writing. Groq retires models
    /// (llama-3.3-70b went on 16 August 2026), so check what your account
    /// has with <c>Plannr.Eval models</c> and change it here, not in code.
    /// </summary>
    public string TextModel { get; set; } = "qwen/qwen3.8-27b";

    /// <summary>
    /// A model that accepts images, for screenshots. Empty until checked
    /// with <c>Plannr.Eval models</c>: no default is guessed, because a
    /// wrong guess fails only when the first screenshot is shared.
    /// </summary>
    public string VisionModel { get; set; } = string.Empty;

    /// <summary>
    /// Sent as reasoning_effort when set. Empty: chosen per model, as in
    /// Applr -- "none" for Qwen, "low" for gpt-oss (which can't turn it
    /// off), left out for models that reject the field.
    /// </summary>
    public string ReasoningEffort { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>The answer is one small JSON object; Groq counts this allowance against the rate limit whatever is written.</summary>
    public int MaxOutputTokens { get; set; } = 1024;

    /// <summary>0 so the same page gives the same answer -- needed for a fair accuracy measurement.</summary>
    public double Temperature { get; set; }

    /// <summary>How many times a 429 (rate limited) is waited out before giving up.</summary>
    public int RateLimitRetries { get; set; } = 3;

    /// <summary>The longest single wait for a rate limit, whatever Retry-After asks for.</summary>
    public int MaxRateLimitWaitSeconds { get; set; } = 30;

    /// <summary>The reasoning_effort to send for <paramref name="model"/>, or null to leave the field out.</summary>
    public string? ReasoningEffortFor(string model) =>
        !string.IsNullOrWhiteSpace(ReasoningEffort) ? ReasoningEffort
        : model.StartsWith("qwen/", StringComparison.Ordinal) ? "none"
        : model.StartsWith("openai/gpt-oss", StringComparison.Ordinal) ? "low"
        : null;

    public override void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(ApiKey)} is not set. From the program's project folder run " +
                $"dotnet user-secrets set \"{SectionName}:{nameof(ApiKey)}\" \"<your key>\".");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUrl) || baseUrl.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(BaseUrl)} must be an absolute http(s) URL, not '{BaseUrl}'.");
        }

        if (string.IsNullOrWhiteSpace(TextModel))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(TextModel)} must be set.");
        }

        if (TimeoutSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(TimeoutSeconds)} must be between 1 and 300, not {TimeoutSeconds}.");
        }

        if (MaxOutputTokens is < 100 or > 8000)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MaxOutputTokens)} must be between 100 and 8000, not {MaxOutputTokens}.");
        }

        if (Temperature is < 0 or > 2)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(Temperature)} must be between 0 and 2, not {Temperature}.");
        }

        if (RateLimitRetries is < 0 or > 10)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(RateLimitRetries)} must be between 0 and 10, not {RateLimitRetries}.");
        }

        if (MaxRateLimitWaitSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MaxRateLimitWaitSeconds)} must be between 1 and 300, not {MaxRateLimitWaitSeconds}.");
        }
    }
}
