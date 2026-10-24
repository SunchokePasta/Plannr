using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Plannr.Api.Controllers;
using Plannr.Api.HandOffs;
using Plannr.Api.Notifications;
using Plannr.Api.Options;
using Plannr.Data.Entities;
using Plannr.Data.Repositories;
using Plannr.Services.Diagnostics;
using Plannr.Services.DTOs;
using Plannr.Services.HandOffs;
using Plannr.Tests.Diagnostics;

namespace Plannr.Tests.HandOffs;

/// <summary>H5: the API's side of the result hand-off.</summary>
[Collection(ErrorReferenceCollection.Name)]
public sealed class ApiResultHandOffTests : IDisposable
{
    private readonly FakeCaptureRepository _captures = new();
    private readonly FakeCardRepository _cards = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeTimeProvider _time = new();
    private readonly HandOffOptions _options = new() { Secret = HandOffTestData.Secret };
    private readonly WakeSignal _announcements;

    public ApiResultHandOffTests()
    {
        ErrorReference.Configure(ErrorReference.ApiProgram, ErrorReference.MainLog);
        _announcements = new WakeSignal(_time);
    }

    public void Dispose() => ErrorReference.Reset();

    private HandOffsController Controller() => new(_captures, _cards, _announcements, _options);

    private Capture Processing(string userId = "user-1", CaptureStatus status = CaptureStatus.Processing)
    {
        return _captures.Add(new Capture
        {
            Id = _captures.Saved.Count + 1, UserId = userId, SourceUrl = HandOffTestData.Link, Status = status
        });
    }

    [Fact(Skip = Todo.H5_1)]
    public async Task ACard_IsSavedForTheCapturesUserAndTheAnnouncerIsNudged()
    {
        var capture = Processing("user-7");

        var answer = await Controller().ReportResultAsync(capture.Id, CaptureResult.Succeeded(HandOffTestData.Card()), CancellationToken.None);

        Assert.IsType<OkResult>(answer);
        var (userId, draft) = Assert.Single(_cards.Added);
        Assert.Equal("user-7", userId);
        Assert.Equal("Thrift Market", draft.Title);
        Assert.Equal((capture.Id, 101L), Assert.Single(_captures.Completed));
        Assert.True(await _announcements.WasNudgedAsync());
    }

    [Fact(Skip = Todo.H5_1)]
    public async Task AFailure_IsSavedWithItsMessageAndReference()
    {
        var capture = Processing();

        await Controller().ReportResultAsync(capture.Id, CaptureResult.Failed("That page has gone.", "7f3a9c21:02:01"), CancellationToken.None);

        Assert.Equal((capture.Id, "That page has gone.", "7f3a9c21:02:01"), Assert.Single(_captures.FailedCalls));
        Assert.Empty(_cards.Added);
    }

    [Theory(Skip = Todo.H5_1)]
    [InlineData(CaptureStatus.Done)]
    [InlineData(CaptureStatus.Failed)]
    public async Task ARepeatedResult_ChangesNothingAndIsStill200(CaptureStatus finished)
    {
        var capture = Processing();
        capture.Status = finished;

        var answer = await Controller().ReportResultAsync(capture.Id, CaptureResult.Succeeded(HandOffTestData.Card()), CancellationToken.None);

        Assert.IsType<OkResult>(answer);
        Assert.Empty(_cards.Added);
        Assert.Empty(_captures.Completed);
        Assert.False(await _announcements.WasNudgedAsync());
    }

    [Fact(Skip = Todo.H5_1)]
    public async Task AnUnknownCapture_Is404()
    {
        var answer = await Controller().ReportResultAsync(99, CaptureResult.Succeeded(HandOffTestData.Card()), CancellationToken.None);

        Assert.IsType<NotFoundResult>(answer);
    }

    [Fact(Skip = Todo.H5_1)]
    public async Task NeitherOrBoth_Is400()
    {
        var capture = Processing();
        var both = CaptureResult.Succeeded(HandOffTestData.Card()) with { Failure = new CaptureFailure("x", "y") };

        Assert.IsType<BadRequestResult>(await Controller().ReportResultAsync(capture.Id, new CaptureResult(), CancellationToken.None));
        Assert.IsType<BadRequestResult>(await Controller().ReportResultAsync(capture.Id, both, CancellationToken.None));
    }

    private ResultAnnouncer Announcer(FakeLogger<ResultAnnouncer>? logger = null)
    {
        var scopes = new ServiceCollection()
            .AddSingleton<ICaptureRepository>(_captures)
            .AddSingleton<INotificationService>(_notifications)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        return new ResultAnnouncer(scopes, _announcements, _options, logger ?? new FakeLogger<ResultAnnouncer>());
    }

    private Capture Finished(CaptureStatus status = CaptureStatus.Done) => Processing(status: status);

    [Fact(Skip = Todo.H5_3)]
    public async Task TheAnnouncer_TellsEachFinishedCapturesUserAndMarksIt()
    {
        var done = Finished();
        var failed = Finished(CaptureStatus.Failed);
        Processing();

        var announced = await Announcer().AnnouncePendingAsync(CancellationToken.None);

        Assert.Equal(2, announced);
        Assert.Equal(new[] { done.Id, failed.Id }, _notifications.Told);
        Assert.Equal(new[] { done.Id, failed.Id }, _captures.Announced);
    }

    [Fact(Skip = Todo.H5_3)]
    public async Task OneFailedAnnouncement_DoesntStopTheOthers_AndIsLeftForTheNextSweep()
    {
        var first = Finished();
        var second = Finished();
        _notifications.FailFor.Add(first.Id);
        var logger = new FakeLogger<ResultAnnouncer>();

        await Announcer(logger).AnnouncePendingAsync(CancellationToken.None);

        Assert.Equal(new[] { second.Id }, _notifications.Told);
        Assert.Equal(new[] { second.Id }, _captures.Announced);
        Assert.Equal(LogLevel.Warning, logger.Collector.LatestRecord.Level);
        Assert.Matches(@"^\[[0-9a-f]{8}:01:01\]", logger.Collector.LatestRecord.Message);
    }

    [Fact(Skip = Todo.H5_2)]
    public async Task TheAnnouncerLoop_RunsAtStartOnANudgeAndOnTheSweep()
    {
        // Left over from before a restart: announced as soon as it starts.
        var leftOver = Finished();
        var announcer = Announcer();
        await announcer.StartAsync(CancellationToken.None);

        try
        {
            await WaitUntil(() => _notifications.Told.Contains(leftOver.Id));

            // A nudge (no time passes on the fake clock, so only the nudge can wake it).
            var nudged = Finished();
            _announcements.Nudge();
            await WaitUntil(() => _notifications.Told.Contains(nudged.Id));

            // No nudge: only the sweep finds it. Moving the clock on each check
            // covers the moment before the loop has started waiting again.
            var quiet = Finished();
            await WaitUntil(() =>
            {
                _time.Advance(_options.AnnouncementSweep);
                return _notifications.Told.Contains(quiet.Id);
            });
        }
        finally
        {
            await announcer.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the announcer.");
            await Task.Delay(10);
        }
    }
}
