namespace Plannr.Tests.Fixtures;

/// <summary>The synthetic pages in Fixtures/Pages (see the README there), copied beside the test DLL.</summary>
internal static class FixturePages
{
    public const string Ticketing = "ticketing-aggregate-offer.html";
    public const string MusicVenue = "music-venue-graph.html";
    public const string WordPress = "wordpress-events-calendar.html";
    public const string WhatsOn = "venue-whats-on-list.html";
    public const string Restaurant = "restaurant-local-business.html";
    public const string Blog = "blog-no-structured-data.html";
    public const string JavaScriptShell = "javascript-shell.html";
    public const string BrokenJsonLd = "broken-json-ld.html";

    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Pages", name));

    /// <summary>A plausible address for each page, so retrieval and logs look like the real thing.</summary>
    public static Uri UrlFor(string name) => new($"https://fixtures.example/{Path.GetFileNameWithoutExtension(name)}");
}
