using System.Globalization;
using System.Text.Json;
using Plannr.Core.Capture;

namespace Plannr.Infrastructure.Llm;

/// <summary>
/// The model's JSON answer -> <see cref="LlmCardFields"/>. Forgiving about
/// the small ways models drift from the asked-for shape (a bare value with
/// no confidence, "starts_at" for "startsAt", a number where text was
/// asked for, a ```json fence), and strict about the big one: an answer
/// that isn't a JSON object at all is a failure, not an empty card.
/// </summary>
internal static class LlmAnswerParser
{
    /// <summary>
    /// What a field the model gave without a confidence counts as: below the
    /// normaliser's flag threshold, so the user is asked to check it.
    /// </summary>
    public const double DefaultConfidence = 0.5;

    private static readonly JsonDocumentOptions Lenient = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    /// <exception cref="JsonException">Not JSON.</exception>
    /// <exception cref="FormatException">JSON, but not an object.</exception>
    public static LlmCardFields Parse(string raw)
    {
        using var document = JsonDocument.Parse(StripFence(raw), Lenient);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"Expected a JSON object, got {root.ValueKind}.");
        }

        var fields = root.EnumerateObject()
            .GroupBy(p => Normalise(p.Name))
            .ToDictionary(g => g.Key, g => g.First().Value);

        return new LlmCardFields
        {
            Title = Field(fields, "title"),
            Description = Field(fields, "description"),
            StartsAt = Field(fields, "startsat"),
            EndsAt = Field(fields, "endsat"),
            LocationName = Field(fields, "locationname"),
            Address = Field(fields, "address"),
            Price = Field(fields, "price"),
            Category = Field(fields, "category")
        };
    }

    /// <summary>"startsAt", "starts_at" and "StartsAt" are the same field.</summary>
    private static string Normalise(string name) => name.Replace("_", string.Empty).ToLowerInvariant();

    private static LlmField? Field(Dictionary<string, JsonElement> fields, string name)
    {
        if (!fields.TryGetValue(name, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var value = element.TryGetProperty("value", out var v) ? Text(v) : null;
            var confidence = element.TryGetProperty("confidence", out var c) ? Confidence(c) : null;

            return value is null ? null : new LlmField(value, confidence ?? DefaultConfidence);
        }

        var bare = Text(element);
        return bare is null ? null : new LlmField(bare, DefaultConfidence);
    }

    private static string? Text(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()) ? null : element.GetString()!.Trim(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
        _ => null
    };

    private static double? Confidence(JsonElement element)
    {
        double? value = element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };

        return value is null ? null : Math.Clamp(value.Value, 0, 1);
    }

    /// <summary>JSON mode should prevent fences, but a model that adds one anyway shouldn't fail the capture.</summary>
    private static string StripFence(string raw)
    {
        var text = raw.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLineEnd = text.IndexOf('\n');
        var closing = text.LastIndexOf("```", StringComparison.Ordinal);
        return firstLineEnd > 0 && closing > firstLineEnd ? text[(firstLineEnd + 1)..closing] : text;
    }
}
