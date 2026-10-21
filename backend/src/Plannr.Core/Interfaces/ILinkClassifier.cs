using Plannr.Core.Capture;

namespace Plannr.Core.Interfaces;

/// <summary>
/// Decides a shared URL's <see cref="SourceType"/> from the URL alone -- no
/// network calls, so it is instant and can run inside POST /captures before
/// anything is queued.
/// </summary>
public interface ILinkClassifier
{
    /// <exception cref="Exceptions.UnsupportedLinkException">
    /// Not http(s), or a kind of link Plannr doesn't read yet.
    /// </exception>
    SourceType Classify(Uri url);
}
