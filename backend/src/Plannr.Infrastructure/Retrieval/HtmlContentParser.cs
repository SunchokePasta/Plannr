using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using Plannr.Core.Capture;
using Plannr.Infrastructure.Options;

namespace Plannr.Infrastructure.Retrieval;

/// <summary>
/// Reads an HTML page into <see cref="RetrievedContent"/>: Open Graph tags,
/// schema.org JSON-LD and the main text. Knows nothing about HTTP, so the
/// same code reads a live page, a page Playwright rendered, and a snapshot
/// saved in the data pool -- which is what lets its tests run offline.
/// </summary>
public sealed partial class HtmlContentParser(RetrievalOptions options, ILogger<HtmlContentParser> logger)
{
    /// <summary>Elements whose text is never part of what a page is about.</summary>
    private const string NoiseSelector = "script, style, noscript, template, svg, nav, header, footer, iframe";

    /// <summary>
    /// schema.org types kept for their location: venues and businesses
    /// carry a full address even when the page has no Event. Not every Place
    /// subtype -- just the ones activities happen at.
    /// </summary>
    private static readonly HashSet<string> PlaceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Place", "LocalBusiness", "EventVenue", "MusicVenue", "PerformingArtsTheater", "MovieTheater",
        "NightClub", "BarOrPub", "Restaurant", "CafeOrCoffeeShop", "FoodEstablishment", "Museum",
        "ArtGallery", "TouristAttraction", "Park", "StadiumOrArena", "SportsActivityLocation", "Store",
        "AmusementPark", "Zoo", "Aquarium"
    };

    private static readonly JsonDocumentOptions LenientJson = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public RetrievedContent Parse(string html, Uri pageUrl, SourceType sourceType)
    {
        var document = new HtmlParser().ParseDocument(html);

        var (structuredData, hasEventData) = ReadJsonLd(document, pageUrl);

        return new RetrievedContent
        {
            SourceUrl = pageUrl,
            SourceType = sourceType,
            Title = Meta(document, "og:title") ?? Clean(document.Title),
            Description = Meta(document, "og:description") ?? Meta(document, "description"),
            ThumbnailUrl = AbsoluteHttpUrl(Meta(document, "og:image"), pageUrl),
            StructuredData = structuredData,
            HasEventData = hasEventData,
            BodyText = ReadBodyText(document)
        };
    }

    /// <summary>
    /// Some sites put og: tags in name= rather than property=, so both are
    /// read. Empty content counts as missing.
    /// </summary>
    private static string? Meta(IDocument document, string key) =>
        Clean(document.QuerySelector($"meta[property='{key}'], meta[name='{key}']")?.GetAttribute("content"));

    private static Uri? AbsoluteHttpUrl(string? value, Uri pageUrl) =>
        value is not null
        && Uri.TryCreate(pageUrl, value, out var url)
        && url.Scheme is "http" or "https"
            ? url
            : null;

    /// <summary>
    /// Every Event (and each Place, for its address) from every JSON-LD
    /// block, as one JSON array: events first, so if the size limit cuts
    /// anything it's the venue details, not the event.
    /// </summary>
    private (string? Json, bool HasEventData) ReadJsonLd(IDocument document, Uri pageUrl)
    {
        var events = new List<JsonNode>();
        var places = new List<JsonNode>();

        var blocks = document.QuerySelectorAll("script")
            .Where(s => string.Equals(s.GetAttribute("type")?.Trim(), "application/ld+json", StringComparison.OrdinalIgnoreCase));

        foreach (var block in blocks)
        {
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(block.TextContent, documentOptions: LenientJson);
            }
            catch (JsonException exception)
            {
                // Hand-written JSON-LD is often broken (stray commas, HTML
                // entities). One bad block isn't a failed capture -- skip it
                // and read the rest of the page.
                logger.LogDebug("Skipped an unreadable JSON-LD block on {Url}: {Error}", pageUrl, exception.Message);
                continue;
            }

            Collect(root, events, places);
        }

        if (events.Count == 0 && places.Count == 0)
        {
            return (null, false);
        }

        var kept = new JsonArray();
        var length = 2;
        foreach (var node in events.Concat(places))
        {
            var json = node.ToJsonString();

            // Always keep the first object, even when it alone is over the
            // limit: one oversized event is still the best thing on the page.
            if (kept.Count > 0 && length + json.Length > options.MaxStructuredDataChars)
            {
                logger.LogDebug(
                    "JSON-LD on {Url} is over {Limit} characters; kept {Kept} of {Total} objects.",
                    pageUrl, options.MaxStructuredDataChars, kept.Count, events.Count + places.Count);
                break;
            }

            kept.Add(node.DeepClone());
            length += json.Length + 1;
        }

        return (kept.ToJsonString(), events.Count > 0);
    }

    /// <summary>
    /// Walks one JSON-LD value. The same data turns up as a single object,
    /// an array of objects, or an object whose "@graph" holds the rest.
    /// </summary>
    private static void Collect(JsonNode? node, List<JsonNode> events, List<JsonNode> places)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var item in array)
                {
                    Collect(item, events, places);
                }

                break;

            case JsonObject obj:
                if (obj["@graph"] is { } graph)
                {
                    Collect(graph, events, places);
                }

                var types = TypesOf(obj).ToList();
                if (types.Any(IsEventType))
                {
                    events.Add(obj);
                }
                else if (types.Any(PlaceTypes.Contains))
                {
                    places.Add(obj);
                }

                break;
        }
    }

    /// <summary>
    /// "@type" may be one string or an array, and may be written in full
    /// ("http://schema.org/MusicEvent") or prefixed ("schema:Event"); only
    /// the last part is the type name.
    /// </summary>
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

    /// <summary>schema.org's Event subtypes all end in "Event" (MusicEvent, ComedyEvent...), apart from Festival.</summary>
    private static bool IsEventType(string type) =>
        type.EndsWith("Event", StringComparison.OrdinalIgnoreCase)
        || type.Equals("Festival", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The page's readable text: noise elements removed, &lt;main&gt; or
    /// &lt;article&gt; preferred over the whole body, text nodes joined with
    /// spaces (TextContent would run "&lt;p&gt;Sat&lt;/p&gt;&lt;p&gt;7pm&lt;/p&gt;" into
    /// "Sat7pm"), then cut to the limit at a word boundary.
    /// </summary>
    private string? ReadBodyText(IDocument document)
    {
        foreach (var noise in document.QuerySelectorAll(NoiseSelector).ToList())
        {
            noise.Remove();
        }

        var root = document.QuerySelector("main") ?? document.QuerySelector("article") ?? document.Body;
        if (root is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var text in root.Descendants<IText>())
        {
            builder.Append(text.Data).Append(' ');
        }

        var body = Clean(builder.ToString());
        if (body is null || body.Length <= options.MaxBodyTextChars)
        {
            return body;
        }

        var cut = body.LastIndexOf(' ', options.MaxBodyTextChars);
        return body[..(cut > 0 ? cut : options.MaxBodyTextChars)];
    }

    /// <summary>Whitespace collapsed and trimmed; empty becomes null.</summary>
    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
