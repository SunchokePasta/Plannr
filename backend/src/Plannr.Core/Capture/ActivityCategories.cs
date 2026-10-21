namespace Plannr.Core.Capture;

/// <summary>
/// The fixed set of categories a card can have. Fixed so the wishlist can
/// filter on them; the LLM is told to pick one, and anything else it says
/// becomes <see cref="Other"/>.
/// </summary>
public static class ActivityCategories
{
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All =
    [
        "music",
        "nightlife",
        "food-drink",
        "markets",
        "arts-culture",
        "comedy",
        "film-tv",
        "sports-fitness",
        "outdoors",
        "workshops-classes",
        "community",
        Other
    ];

    public static bool IsKnown(string? category) =>
        category is not null && All.Contains(category, StringComparer.Ordinal);
}
