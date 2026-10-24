using Plannr.Api.HandOffs;
using Plannr.Data.Entities;
using Plannr.Data.Repositories;
using Plannr.Services.Interfaces;

namespace Plannr.Api.Services;

/// <summary>
/// The start of the job hand-off (D37): what POST /captures does with a
/// shared link. It only ever takes milliseconds: the slow part (the website,
/// Groq) is the worker's.
/// </summary>
public sealed class CaptureService(ILinkClassifier classifier, ICaptureRepository captures, IWorkerNudger worker)
{
    // What dependency injection handed the constructor, kept for the methods below.
    private readonly ILinkClassifier _classifier = classifier;
    private readonly ICaptureRepository _captures = captures;
    private readonly IWorkerNudger _worker = worker;

    /// <summary>
    /// Refuses a link Plannr can't read at once, saves the rest as Waiting,
    /// and nudges the worker.
    /// </summary>
    /// <exception cref="Plannr.Services.Exceptions.UnsupportedLinkException">
    /// Not a link Plannr can read; the middleware answers 400 with its UserMessage. Nothing is saved.
    /// </exception>
    public Task<Capture> AcceptLinkAsync(string userId, Uri link, CancellationToken cancellationToken = default)
    {
        // TODO(H3.1): three steps, in this order (make the method async):
        //   1. _classifier.Classify(link): throws UnsupportedLinkException for a link Plannr can't read,
        //      before anything is saved. Its return value isn't needed here; the worker classifies again.
        //   2. Save it: _captures.AddAsync(userId, link, cancellationToken). It starts out Waiting.
        //   3. Nudge the worker (_worker.Nudge()). Only after the save: a worker woken before the row
        //      exists would find nothing to claim.
        //   Return the saved capture.
        throw new NotImplementedException("TODO(H3.1): see docs/guides/hand-offs.md");
    }
}
