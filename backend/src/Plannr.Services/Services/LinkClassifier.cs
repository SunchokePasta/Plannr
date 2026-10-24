using Plannr.Services.DTOs;
using Plannr.Services.Exceptions;
using Plannr.Services.Interfaces;

namespace Plannr.Services.Services;

/// <summary>
/// Decides a link's <see cref="SourceType"/> from its host alone, without
/// touching the network, so it's instant and safe to run on the server the
/// moment a link arrives. ArchitectureTests keeps it that way: only APIs/
/// may make network calls.
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
