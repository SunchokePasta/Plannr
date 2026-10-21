using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Plannr.Core.Capture;

/// <summary>
/// Maps the well-formed parts of a page's schema.org JSON-LD straight onto
/// card fields, with no LLM involved -- the spec's "reads the event details
/// many sites embed; the LLM fills gaps". Code can't misread an ISO date or
/// a numeric price, and every field mapped here is one the LLM isn't relied on for.
///
/// Only takes what it can trust: a price written as "£5 - £10" text, or a
/// location given as a bare URL, is left for the LLM rather than guessed at.
/// Input is HtmlContentParser's array of Event and Place objects.
/// </summary>
public static partial class JsonLdEventMapper
{
    private const int MaxDescriptionChars = 1000;

    public static StructuredFields? Map(string? structuredData, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(structuredData))
        {
            return null;
        }

        JsonArray objects;
        try
        {
            objects = JsonNode.Parse(structuredData) as JsonArray ?? [];
        }
        catch (JsonException)
        {
            return null;
        }

        var events = objects.OfType<JsonObject>().Where(o => TypesOf(o).Any(IsEventType)).ToList();
        var place = objects.OfType<JsonObject>().FirstOrDefault(o => !TypesOf(o).Any(IsEventType));

        var chosen = ChooseEvent(events, now);
        if (chosen is null && place is null)
        {
            return null;
        }

        var (start, startHasTime) = Date(chosen?["startDate"]);
        var (end, endHasTime) = Date(chosen?["endDate"]);
        var location = chosen?["location"] ?? place;
        var (min, max, currency) = Price(chosen?["offers"]);

        return new StructuredFields
        {
            Title = Text(chosen?["name"]),
            Description = Description(chosen?["description"]),
            StartsAt = start,
            StartTimeKnown = startHasTime,
            EndsAt = end,
            EndTimeKnown = endHasTime,
            LocationName = LocationName(location),
            Address = Address(location),
            PriceMinPence = min,
            PriceMaxPence = max,
            Currency = currency
        };
    }

    /// <summary>
    /// A "what's on" page can list many events. The first one that hasn't
    /// started yet (or has no readable date) is the most likely to be the
    /// one shared; if they're all past, the first.
    /// </summary>
    private static JsonObject? ChooseEvent(List<JsonObject> events, DateTimeOffset now)
    {
        // Midnight today in UK time, built explicitly: comparing against
        // now.Date (a DateTime) would silently use the machine's own zone.
        var startOfToday = UkTime.FromLocal(TimeZoneInfo.ConvertTime(now, UkTime.Zone).Date);

        return events.FirstOrDefault(e =>
            {
                var (start, _) = Date(e["startDate"]);
                return start is null || start >= startOfToday;
            })
            ?? events.FirstOrDefault();
    }

    private static (DateTimeOffset? Value, bool HasTime) Date(JsonNode? node) =>
        UkDateParser.TryParse(Text(node), out var value, out var hasTime) ? (value, hasTime) : (null, false);

    /// <summary>
    /// offers can be one Offer, an AggregateOffer (lowPrice/highPrice), or an
    /// array of either. Only numeric prices are taken; anything that needs
    /// reading as text goes to the LLM.
    /// </summary>
    private static (int? Min, int? Max, string? Currency) Price(JsonNode? offers)
    {
        var prices = new List<int>();
        string? currency = null;

        foreach (var offer in AsObjects(offers))
        {
            foreach (var key in new[] { "price", "lowPrice", "highPrice" })
            {
                if (Pence(offer[key]) is { } pence)
                {
                    prices.Add(pence);
                }
            }

            currency ??= Text(offer["priceCurrency"])?.ToUpperInvariant();
        }

        if (prices.Count == 0)
        {
            return (null, null, null);
        }

        // A price with no currency on a UK site is very probably pounds, but
        // "probably" isn't this mapper's job: leave it to the LLM.
        return currency is { Length: 3 } ? (prices.Min(), prices.Max(), currency) : (null, null, null);
    }

    private static int? Pence(JsonNode? node)
    {
        var text = node switch
        {
            JsonValue value when value.TryGetValue<decimal>(out var number) => number.ToString(CultureInfo.InvariantCulture),
            _ => Text(node)
        };

        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) && amount >= 0
            ? (int)Math.Round(amount * 100, MidpointRounding.AwayFromZero)
            : null;
    }

    private static string? LocationName(JsonNode? location)
    {
        var first = AsObjects(location).FirstOrDefault();
        if (first is null)
        {
            return null;
        }

        if (TypesOf(first).Contains("VirtualLocation"))
        {
            return "Online";
        }

        return Text(first["name"]);
    }

    /// <summary>address is a PostalAddress object or, on less careful sites, a single string.</summary>
    private static string? Address(JsonNode? location)
    {
        var first = AsObjects(location).FirstOrDefault();
        var node = first?["address"];

        if (node is JsonValue)
        {
            return Text(node);
        }

        if (node is not JsonObject postal)
        {
            return null;
        }

        // Sites often put the whole address in streetAddress and repeat the
        // town and postcode in their own fields ("..., London, W6 0RF" +
        // "London"), so a part already in the address isn't added again.
        var address = string.Empty;
        foreach (var part in new[] { "streetAddress", "addressLocality", "postalCode" }.Select(key => Text(postal[key])).OfType<string>())
        {
            if (!address.Contains(part, StringComparison.OrdinalIgnoreCase))
            {
                address = address.Length == 0 ? part : $"{address}, {part}";
            }
        }

        return address.Length == 0 ? null : address;
    }

    /// <summary>JSON-LD descriptions often carry HTML and entities; the card wants plain text.</summary>
    private static string? Description(JsonNode? node)
    {
        var text = Text(node);
        if (text is null)
        {
            return null;
        }

        text = WebUtility.HtmlDecode(Tags().Replace(text, " "));
        text = Whitespace().Replace(text, " ").Trim();

        if (text.Length == 0)
        {
            return null;
        }

        return text.Length <= MaxDescriptionChars ? text : text[..MaxDescriptionChars].TrimEnd() + "...";
    }

    private static IEnumerable<JsonObject> AsObjects(JsonNode? node) => node switch
    {
        JsonObject obj => [obj],
        JsonArray array => array.OfType<JsonObject>(),
        _ => []
    };

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? WebUtility.HtmlDecode(text).Trim()
            : null;

    private static IEnumerable<string> TypesOf(JsonObject obj)
    {
        var values = obj["@type"] switch
        {
            JsonValue value when value.TryGetValue<string>(out var single) => [single],
            JsonArray array => array.OfType<JsonValue>()
                .Select(v => v.TryGetValue<string>(out var s) ? s : null)
                .OfType<string>(),
            _ => []
        };

        return values.Select(v => v[(v.LastIndexOfAny(['/', ':', '#']) + 1)..]);
    }

    private static bool IsEventType(string type) =>
        type.EndsWith("Event", StringComparison.OrdinalIgnoreCase)
        || type.Equals("Festival", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
