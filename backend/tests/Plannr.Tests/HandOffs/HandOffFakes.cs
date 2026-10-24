using Plannr.Api.HandOffs;
using Plannr.Api.Notifications;
using Plannr.Data.Entities;
using Plannr.Data.Repositories;
using Plannr.Services.APIs;
using Plannr.Services.DTOs;
using Plannr.Services.HandOffs;
using Plannr.Services.Interfaces;

namespace Plannr.Tests.HandOffs;

// Stand-ins for everything the hand-off classes talk to. Deliberately dumb:
// they hold what the test puts in and record what they're asked, so each
// test decides exactly what the other side "did". The real rules (which
// capture a claim takes, leases) belong to the MySQL repository and are
// tested against MySQL (H7).

/// <summary>
/// Captures in a list; the next claim hands out whatever the test queued.
/// Locked throughout, because the announcer's loop reads it from another
/// thread while a test adds to it.
/// </summary>
public sealed class FakeCaptureRepository : ICaptureRepository
{
    private readonly Lock _lock = new();

    public List<Capture> Saved { get; } = [];

    public Queue<Capture?> NextClaims { get; } = new();

    public List<(string WorkerId, TimeSpan Lease, int MaxAttempts)> ClaimCalls { get; } = [];

    public List<(long CaptureId, long CardId)> Completed { get; } = [];

    public List<(long CaptureId, string UserMessage, string Reference)> FailedCalls { get; } = [];

    public List<long> Announced { get; } = [];

    /// <summary>Adds a capture the way a test sets one up, under the same lock the loop reads with.</summary>
    public Capture Add(Capture capture)
    {
        lock (_lock)
        {
            Saved.Add(capture);
            return capture;
        }
    }

    public Task<Capture> AddAsync(string userId, Uri sourceUrl, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(Add(new Capture { Id = Saved.Count + 1, UserId = userId, SourceUrl = sourceUrl }));
        }
    }

    public Task<Capture?> FindAsync(long captureId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(Saved.SingleOrDefault(c => c.Id == captureId));
        }
    }

    public Task<Capture?> TryClaimNextAsync(string workerId, TimeSpan lease, int maxAttempts, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            ClaimCalls.Add((workerId, lease, maxAttempts));
            return Task.FromResult(NextClaims.Count > 0 ? NextClaims.Dequeue() : null);
        }
    }

    public Task<bool> CompleteAsync(long captureId, long cardId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            Completed.Add((captureId, cardId));
            return Task.FromResult(true);
        }
    }

    public Task<bool> FailAsync(long captureId, string userMessage, string errorReference, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            FailedCalls.Add((captureId, userMessage, errorReference));
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<Capture>> FindUnannouncedAsync(int max, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Capture>>(Saved
                .Where(c => c.Status is CaptureStatus.Done or CaptureStatus.Failed && c.AnnouncedAt is null && !Announced.Contains(c.Id))
                .Take(max)
                .ToList());
        }
    }

    public Task MarkAnnouncedAsync(long captureId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            Announced.Add(captureId);
            return Task.CompletedTask;
        }
    }
}

public sealed class FakeCardRepository : ICardRepository
{
    public List<(string UserId, CardDraft Draft)> Added { get; } = [];

    public Task<long> AddFromDraftAsync(string userId, CardDraft draft, CancellationToken cancellationToken = default)
    {
        Added.Add((userId, draft));
        return Task.FromResult(100L + Added.Count);
    }
}

/// <summary>Records who was told; fails for the capture ids the test lists. Locked, like the repository.</summary>
public sealed class FakeNotificationService : INotificationService
{
    private readonly Lock _lock = new();
    private readonly List<long> _told = [];

    public HashSet<long> FailFor { get; } = [];

    public long[] Told
    {
        get
        {
            lock (_lock)
            {
                return [.. _told];
            }
        }
    }

    public Task CaptureFinishedAsync(Capture capture, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (FailFor.Contains(capture.Id))
            {
                throw new InvalidOperationException($"Push service down for capture {capture.Id}.");
            }

            _told.Add(capture.Id);
            return Task.CompletedTask;
        }
    }
}

public sealed class FakeWorkerNudger : IWorkerNudger
{
    public int Nudges { get; private set; }

    public void Nudge() => Nudges++;
}

/// <summary>The worker's wake endpoint, answering however the test says.</summary>
public sealed class FakeWorkerWakeApi(Func<CancellationToken, Task> answer) : IWorkerWakeApi
{
    public int Calls { get; private set; }

    public Task WakeAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        return answer(cancellationToken);
    }
}

/// <summary>The API's /internal endpoints, as the worker sees them.</summary>
public sealed class FakePlannrInternalApi : IPlannrInternalApi
{
    private readonly Lock _lock = new();

    public Queue<ClaimedCapture?> Jobs { get; } = new();

    /// <summary>Queues a job for the next claim, under the lock the worker's loop claims with.</summary>
    public void AddJob(ClaimedCapture job)
    {
        lock (_lock)
        {
            Jobs.Enqueue(job);
        }
    }

    /// <summary>Thrown by each ClaimAsync call in turn, until it runs out (the API restarting, say).</summary>
    public Queue<Exception> ClaimFailures { get; } = new();

    /// <summary>Thrown by each ReportResultAsync call in turn, until it runs out; then calls succeed.</summary>
    public Queue<Exception> ReportFailures { get; } = new();

    public List<(long CaptureId, CaptureResult Result)> Reported { get; } = [];

    public int Claims { get; private set; }

    public Task<ClaimedCapture?> ClaimAsync(ClaimRequest request, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            Claims++;
            if (ClaimFailures.Count > 0)
            {
                throw ClaimFailures.Dequeue();
            }

            return Task.FromResult(Jobs.Count > 0 ? Jobs.Dequeue() : null);
        }
    }

    public Task ReportResultAsync(long captureId, CaptureResult result, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            Reported.Add((captureId, result));
            if (ReportFailures.Count > 0)
            {
                throw ReportFailures.Dequeue();
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>A pipeline that returns a card or throws, as the test says.</summary>
public sealed class FakePipeline(Func<Uri, CardDraft> answer) : IExtractionPipeline
{
    public Task<CardDraft> ExtractFromLinkAsync(Uri url, CancellationToken cancellationToken = default) =>
        Task.FromResult(answer(url));

    public Task<CardDraft> ExtractFromImageAsync(byte[] image, string declaredMediaType, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Screenshots join the hand-offs in M6.");
}

public static class HandOffTestData
{
    /// <summary>
    /// True if a nudge is waiting on <paramref name="signal"/>: waits on it
    /// briefly with a sweep interval that can't pass. Doesn't depend on how
    /// WaitAsync is written inside, only on what it does.
    /// </summary>
    public static async Task<bool> WasNudgedAsync(this WakeSignal signal)
    {
        using var giveUp = new CancellationTokenSource();
        var waiting = signal.WaitAsync(TimeSpan.FromDays(1), giveUp.Token);
        var first = await Task.WhenAny(waiting, Task.Delay(TimeSpan.FromMilliseconds(300)));
        await giveUp.CancelAsync();
        return first == waiting && await waiting;
    }

    public const string Secret = "a-test-secret-that-is-long-enough-1234";

    public static readonly Uri Link = new("https://venue.example.com/events/thrift-market");

    public static CardDraft Card(string title = "Thrift Market") => new()
    {
        SourceUrl = Link,
        SourceType = SourceType.WebPage,
        Title = title
    };
}
