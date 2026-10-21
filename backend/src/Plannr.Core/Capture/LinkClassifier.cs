using Plannr.Core.Exceptions;
using Plannr.Core.Interfaces;

namespace Plannr.Core.Capture;

/// <summary>
/// Decides a link's <see cref="SourceType"/> from its host alone. Lives in
/// Core rather than Infrastructure because it never touches the network.
///
/// Hosts are matched exactly or as a subdomain ("vm.tiktok.com"), never
/// with Contains: "tiktok.com.example.net" is somebody else's site.
/// </summary>
public sealed class LinkClassifier : ILinkClassifier
{
    private static readonly string[] TikTokHosts = ["tiktok.com"];
    private static readonly string[] YouTubeHosts = ["youtube.com", "youtu.be"];
    private static readonly string[] InstagramHosts = ["instagram.com", "instagr.am"];

    public SourceType Classify(Uri url)
    {
        if (!url.IsAbsoluteUri || url.Scheme is not ("http" or "https"))
        {
            throw new UnsupportedLinkException($"'{url}' is not an http(s) link.");
        }

        // A trailing dot ("tiktok.com.") is the same host to DNS.
        var host = url.IdnHost.TrimEnd('.');

        if (Matches(host, InstagramHosts))
        {
            throw new InstagramNotSupportedException($"'{url}' is an Instagram link.");
        }

        if (Matches(host, TikTokHosts))
        {
            return SourceType.TikTok;
        }

        if (Matches(host, YouTubeHosts))
        {
            return SourceType.YouTube;
        }

        return SourceType.WebPage;
    }

    private static bool Matches(string host, string[] domains) =>
        domains.Any(domain =>
            host.Equals(domain, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
}
