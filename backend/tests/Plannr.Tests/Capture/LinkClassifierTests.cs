using Plannr.Core.Capture;
using Plannr.Core.Exceptions;

namespace Plannr.Tests.Capture;

public sealed class LinkClassifierTests
{
    private readonly LinkClassifier _classifier = new();

    [Theory]
    [InlineData("https://www.tiktok.com/@someone/video/7412345678901234567", SourceType.TikTok)]
    [InlineData("https://vm.tiktok.com/ZGabc123/", SourceType.TikTok)]
    [InlineData("https://vt.tiktok.com/ZSxyz789/", SourceType.TikTok)]
    [InlineData("https://m.tiktok.com/v/7412345678901234567.html", SourceType.TikTok)]
    [InlineData("https://TIKTOK.COM./@someone", SourceType.TikTok)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", SourceType.YouTube)]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ", SourceType.YouTube)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", SourceType.YouTube)]
    [InlineData("https://www.eventbrite.co.uk/e/thrift-market-tickets-123", SourceType.WebPage)]
    [InlineData("http://example.com/whats-on", SourceType.WebPage)]
    public void Classify_KnownHosts(string url, SourceType expected)
    {
        Assert.Equal(expected, _classifier.Classify(new Uri(url)));
    }

    [Theory]
    [InlineData("https://tiktok.com.example.net/video/1")]
    [InlineData("https://nottiktok.com/video/1")]
    [InlineData("https://youtube.community.example/watch")]
    public void Classify_LookAlikeHosts_AreJustWebPages(string url)
    {
        Assert.Equal(SourceType.WebPage, _classifier.Classify(new Uri(url)));
    }

    [Theory]
    [InlineData("ftp://files.example.com/flyer.pdf")]
    [InlineData("file:///C:/Users/someone/flyer.html")]
    [InlineData("mailto:events@example.com")]
    public void Classify_NonHttpLinks_AreUnsupported(string url)
    {
        var exception = Assert.Throws<UnsupportedLinkException>(() => _classifier.Classify(new Uri(url)));

        Assert.Equal(UnsupportedLinkException.DefaultUserMessage, exception.UserMessage);
    }

    [Theory]
    [InlineData("https://www.instagram.com/p/C9abcdef/")]
    [InlineData("https://instagram.com/reel/C9abcdef/")]
    [InlineData("https://instagr.am/p/C9abcdef/")]
    public void Classify_Instagram_SuggestsAScreenshot(string url)
    {
        var exception = Assert.Throws<InstagramNotSupportedException>(() => _classifier.Classify(new Uri(url)));

        Assert.Contains("screenshot", exception.UserMessage);
    }

    [Fact]
    public void Classify_Instagram_IsStillAnUnsupportedLink()
    {
        // What lets the middleware's existing 400 arm catch it.
        Assert.ThrowsAny<UnsupportedLinkException>(() =>
            _classifier.Classify(new Uri("https://www.instagram.com/p/C9abcdef/")));
    }
}
