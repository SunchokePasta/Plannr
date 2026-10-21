using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Plannr.Core.Capture;

namespace Plannr.Eval;

/// <summary>
/// How Eval writes a card: indented, camelCase, enums by name, and "£" left
/// as "£" rather than "£" -- it's for people to read and for the data
/// pool's scoring to compare against hand labels.
/// </summary>
public static class CardJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(CardDraft card) => JsonSerializer.Serialize(card, Options);
}
