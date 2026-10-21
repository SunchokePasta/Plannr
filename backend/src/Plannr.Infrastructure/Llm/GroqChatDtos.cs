using System.Text.Json.Serialization;

namespace Plannr.Infrastructure.Llm;

/// <summary>An OpenAI-compatible chat completion request, as Groq takes it. Ported from Applr's.</summary>
public sealed class GroqChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<GroqChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("response_format")]
    public GroqResponseFormat ResponseFormat { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("max_completion_tokens")]
    public int MaxCompletionTokens { get; set; }

    /// <summary>Only for reasoning models; others reject the field, so it is left out when null.</summary>
    [JsonPropertyName("reasoning_effort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningEffort { get; set; }
}

/// <summary>
/// One message. Content is either a plain string (text) or a list of
/// <see cref="GroqContentPart"/>s (text + image, for the vision model) --
/// both are valid OpenAI-format content, and declaring it as object makes
/// System.Text.Json write whichever it actually holds.
/// </summary>
public sealed class GroqChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public object Content { get; set; } = string.Empty;
}

public sealed class GroqContentPart
{
    /// <summary>"text" or "image_url".</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GroqImageUrl? ImageUrl { get; set; }

    public static GroqContentPart ForText(string text) => new() { Type = "text", Text = text };

    /// <summary>
    /// The image travels inside the request as a data: URL, so it is never
    /// uploaded anywhere or given an address of its own.
    /// </summary>
    public static GroqContentPart ForImage(byte[] bytes, string mediaType) => new()
    {
        Type = "image_url",
        ImageUrl = new GroqImageUrl { Url = $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}" }
    };
}

public sealed class GroqImageUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public sealed class GroqResponseFormat
{
    /// <summary>JSON mode: Groq checks the answer parses as JSON, and rejects it (json_validate_failed) if not.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_object";
}

public sealed class GroqChatResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public List<GroqChoice> Choices { get; set; } = [];

    [JsonPropertyName("usage")]
    public GroqUsage? Usage { get; set; }
}

public sealed class GroqChoice
{
    [JsonPropertyName("message")]
    public GroqResponseMessage Message { get; set; } = new();

    /// <summary>"stop" normally; "length" means the answer was cut off at MaxOutputTokens.</summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>The answer's message. Separate from GroqChatMessage because an answer's content is always a string.</summary>
public sealed class GroqResponseMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public sealed class GroqUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
}

public sealed class GroqModelList
{
    [JsonPropertyName("data")]
    public List<GroqModel> Data { get; set; } = [];
}

public sealed class GroqModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; set; }

    [JsonPropertyName("active")]
    public bool? Active { get; set; }

    [JsonPropertyName("context_window")]
    public int? ContextWindow { get; set; }
}
