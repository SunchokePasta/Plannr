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

    /// <summary>
    /// The longest wait for a rate limit this program will accept. When Groq
    /// asks for longer (a per-day limit, or a heavy per-minute one), the call
    /// fails at once with how long the limit has left, rather than retrying
    /// into the same wall. The default suits a person waiting for a card (the
    /// Worker); Eval's data-pool runs raise it, as Applr's overnight
    /// JobAnalyser does, because nobody is waiting on them.
    /// </summary>
    public int MaxRateLimitWaitSeconds { get; set; } = 30;

    /// <summary>
    /// How many times a network failure, our own timeout, or a 5xx from Groq
    /// is retried. Those are usually momentary; anything else (a 400, a bad
    /// key, a retired model) would fail the same way again.
    /// </summary>
    public int TransientRetries { get; set; } = 3;

    /// <summary>The wait before the first transient retry; each later one waits that much longer (5 s, 10 s, 15 s).</summary>
    public int TransientRetryBaseSeconds { get; set; } = 5;

    /// <summary>The reasoning_effort to send for <paramref name="model"/>, or null to leave the field out.</summary>
    public string? ReasoningEffortFor(string model) =>
        !string.IsNullOrWhiteSpace(ReasoningEffort) ? ReasoningEffort
        : model.StartsWith("qwen/", StringComparison.Ordinal) ? "none"
        : model.StartsWith("openai/gpt-oss", StringComparison.Ordinal) ? "low"
        : null;

    public override void Validate()
    {
        const string HowToSet =
            "From the program's project folder run: dotnet user-secrets set \"Groq:ApiKey\" \"gsk_...\" " +
            "(the key itself inside the quotes, nothing around it).";

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(ApiKey)} is not set. {HowToSet}");
        }

        // The key's value is never put in these messages -- only what's wrong with its shape.
        if (ApiKey.StartsWith('<') || ApiKey.EndsWith('>'))
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(ApiKey)} starts or ends with an angle bracket: the < > from an example were " +
                $"probably kept. Set it again without them. {HowToSet}");
        }

        if (ApiKey != ApiKey.Trim() || ApiKey.Contains('"') || ApiKey.Contains('\''))
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(ApiKey)} has spaces around it or quote marks in it. Set it again. {HowToSet}");
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

        if (RateLimitRetries is < 0 or > 50)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(RateLimitRetries)} must be between 0 and 50, not {RateLimitRetries}.");
        }

        // Up to a day, so a batch run can choose to wait out a daily limit.
        if (MaxRateLimitWaitSeconds is < 1 or > 86_400)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MaxRateLimitWaitSeconds)} must be between 1 and 86400, not {MaxRateLimitWaitSeconds}.");
        }

        if (TransientRetries is < 0 or > 10)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(TransientRetries)} must be between 0 and 10, not {TransientRetries}.");
        }

        if (TransientRetryBaseSeconds is < 1 or > 120)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(TransientRetryBaseSeconds)} must be between 1 and 120, not {TransientRetryBaseSeconds}.");
        }
    }
}
