namespace Plannr.Core.Capture;

/// <summary>
/// What kind of thing was shared, decided from the URL alone (or "it's an
/// image") before anything is fetched. There is deliberately no EventPage:
/// an event page and a general web page look the same from their URL. The
/// difference only shows once the page is fetched and turns out to embed
/// event data -- see <see cref="RetrievedContent.HasEventData"/>.
/// </summary>
public enum SourceType
{
    WebPage,
    TikTok,
    YouTube,
    Image
}
