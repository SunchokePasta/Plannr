using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Plannr.Api.Controllers;
using Plannr.Api.HandOffs;
using Plannr.Api.Options;
using Plannr.Api.Services;
using Plannr.Data.Entities;
using Plannr.Services.DTOs;
using Plannr.Services.Exceptions;
using Plannr.Services.HandOffs;
using Plannr.Services.Services;

namespace Plannr.Tests.HandOffs;

/// <summary>H3: the API's side of the job hand-off.</summary>
public sealed class ApiJobHandOffTests
{
    private readonly FakeCaptureRepository _captures = new();
    private readonly FakeWorkerNudger _nudger = new();
    private readonly HandOffOptions _options = new() { Secret = HandOffTestData.Secret, LeaseMinutes = 7, MaxAttempts = 2 };

    private CaptureService Service() => new(new LinkClassifier(), _captures, _nudger);

    private HandOffsController Controller() =>
        new(_captures, new FakeCardRepository(), new WakeSignal(TimeProvider.System), _options);

    [Fact(Skip = Todo.H3_1)]
    public async Task AcceptingALink_SavesItAndNudgesTheWorker()
    {
        var capture = await Service().AcceptLinkAsync("user-1", HandOffTestData.Link);

        var saved = Assert.Single(_captures.Saved);
        Assert.Same(saved, capture);
        Assert.Equal("user-1", saved.UserId);
        Assert.Equal(HandOffTestData.Link, saved.SourceUrl);
        Assert.Equal(1, _nudger.Nudges);
    }

    [Theory(Skip = Todo.H3_1)]
    [InlineData("ftp://venue.example.com/file")]
    [InlineData("https://www.instagram.com/p/abc/")]
    public async Task AnUnsupportedLink_IsRefusedBeforeAnythingHappens(string link)
    {
        await Assert.ThrowsAnyAsync<UnsupportedLinkException>(() => Service().AcceptLinkAsync("user-1", new Uri(link)));

        Assert.Empty(_captures.Saved);
        Assert.Equal(0, _nudger.Nudges);
    }

    [Fact(Skip = Todo.H3_2)]
    public async Task TheNudge_NeverMakesTheCallerWait()
    {
        var never = new TaskCompletionSource();
        var worker = new FakeWorkerWakeApi(_ => never.Task);

        new WorkerNudger(worker, new FakeLogger<WorkerNudger>()).Nudge();

        // Nudge returned although the worker never answered. The call is under way in the background.
        await WaitUntil(() => worker.Calls == 1);
    }

    [Fact(Skip = Todo.H3_2)]
    public async Task AFailedNudge_IsAWarningNotAnError()
    {
        var logger = new FakeLogger<WorkerNudger>();
        var worker = new FakeWorkerWakeApi(_ => throw new HttpRequestException("Connection refused"));

        new WorkerNudger(worker, logger).Nudge();

        await WaitUntil(() => logger.Collector.Count == 1);
        Assert.Equal(LogLevel.Warning, logger.Collector.LatestRecord.Level);
        Assert.Contains("Connection refused", logger.Collector.LatestRecord.Message);
    }

    [Fact(Skip = Todo.H3_3)]
    public async Task AClaim_HandsOutTheNextCaptureWithTheConfiguredLease()
    {
        _captures.NextClaims.Enqueue(new Capture { Id = 42, SourceUrl = HandOffTestData.Link, Attempts = 1, Status = CaptureStatus.Processing });

        var answer = await Controller().ClaimAsync(new ClaimRequest("worker-a"), CancellationToken.None);

        Assert.Equal(new ClaimedCapture(42, HandOffTestData.Link, 1), answer.Value);
        Assert.Equal(("worker-a", TimeSpan.FromMinutes(7), 2), Assert.Single(_captures.ClaimCalls));
    }

    [Fact(Skip = Todo.H3_3)]
    public async Task AClaimWithNothingWaiting_Is204()
    {
        var answer = await Controller().ClaimAsync(new ClaimRequest("worker-a"), CancellationToken.None);

        Assert.IsType<NoContentResult>(answer.Result);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the background call.");
            await Task.Delay(10);
        }
    }
}
