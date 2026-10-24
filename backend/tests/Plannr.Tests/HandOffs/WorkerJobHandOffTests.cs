using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Plannr.Services.APIs;
using Plannr.Services.DTOs;
using Plannr.Services.HandOffs;
using Plannr.Worker.HandOffs;
using Plannr.Worker.Options;

namespace Plannr.Tests.HandOffs;

/// <summary>H4.1-H4.5: the worker's side of the job hand-off (the wake endpoint and the job loop).</summary>
public sealed class WorkerJobHandOffTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly FakePlannrInternalApi _api = new();
    private readonly FakeJobs _jobs = new();
    private readonly WorkerOptions _options = new() { Secret = HandOffTestData.Secret, Slots = 2 };
    private readonly WakeSignal _signal;

    public WorkerJobHandOffTests() => _signal = new WakeSignal(_time);

    /// <summary>Jobs that run until the test lets them finish.</summary>
    private sealed class FakeJobs : ICaptureJobProcessor
    {
        private readonly Lock _lock = new();
        private readonly List<long> _started = [];

        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Hold { get; set; }

        public long[] Started
        {
            get
            {
                lock (_lock)
                {
                    return [.. _started];
                }
            }
        }

        public async Task ProcessAsync(ClaimedCapture job, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                _started.Add(job.CaptureId);
            }

            if (Hold)
            {
                await Gate.Task.WaitAsync(cancellationToken);
            }
        }
    }

    private static ClaimedCapture Job(long id) => new(id, HandOffTestData.Link, 1);

    private JobLoop Loop() => new(
        new ServiceCollection()
            .AddSingleton<IPlannrInternalApi>(_api)
            .AddSingleton<ICaptureJobProcessor>(_jobs)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>(),
        _signal, _options, new FakeLogger<JobLoop>());

    [Fact(Skip = Todo.H4_1)]
    public async Task TheWakeEndpoint_RingsTheDoorbell()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[SharedSecret.HeaderName] = HandOffTestData.Secret;

        var answer = WakeEndpoint.Handle(request, _signal, _options);

        Assert.IsType<NoContent>(answer);
        Assert.True(await _signal.WasNudgedAsync());
    }

    [Fact(Skip = Todo.H4_1)]
    public async Task TheWakeEndpoint_RefusesAWrongSecret()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[SharedSecret.HeaderName] = "wrong";

        var answer = WakeEndpoint.Handle(request, _signal, _options);

        Assert.IsType<UnauthorizedHttpResult>(answer);
        Assert.False(await _signal.WasNudgedAsync());
    }

    [Fact(Skip = Todo.H4_5)]
    public async Task ARound_ClaimsUntilTheApiHasNothingLeft()
    {
        _api.AddJob(Job(1));

        await Loop().ClaimWhileSlotsFreeAsync(CancellationToken.None);

        await WaitUntil(() => _jobs.Started.Length == 1);
        Assert.Equal(new long[] { 1 }, _jobs.Started);
        Assert.Equal(2, _api.Claims);
    }

    [Fact(Skip = Todo.H4_5)]
    public async Task NoMoreJobsRunAtOnceThanThereAreSlots()
    {
        _jobs.Hold = true;
        foreach (var id in new long[] { 1, 2, 3 })
        {
            _api.AddJob(Job(id));
        }

        var loop = Loop();
        await loop.ClaimWhileSlotsFreeAsync(CancellationToken.None);

        await WaitUntil(() => _jobs.Started.Length == 2);
        Assert.Equal(2, _api.Claims);
        Assert.Equal(0, loop.FreeSlots);

        // Another round while both are busy doesn't even ask.
        await loop.ClaimWhileSlotsFreeAsync(CancellationToken.None);
        Assert.Equal(2, _api.Claims);
    }

    [Fact(Skip = Todo.H4_5)]
    public async Task AFinishedJob_FreesItsSlotAndNudgesTheLoop()
    {
        _jobs.Hold = true;
        _api.AddJob(Job(1));
        var loop = Loop();
        await loop.ClaimWhileSlotsFreeAsync(CancellationToken.None);
        await WaitUntil(() => _jobs.Started.Length == 1);
        Assert.Equal(1, loop.FreeSlots);

        _jobs.Gate.SetResult();

        await WaitUntil(() => loop.FreeSlots == 2);
        Assert.True(await _signal.WasNudgedAsync());
    }

    [Fact(Skip = Todo.H4_5)]
    public async Task AFailedClaim_GivesItsSlotBack()
    {
        _api.ClaimFailures.Enqueue(new HttpRequestException("Connection refused"));
        var loop = Loop();

        await Assert.ThrowsAsync<HttpRequestException>(() => loop.ClaimWhileSlotsFreeAsync(CancellationToken.None));

        Assert.Equal(2, loop.FreeSlots);
    }

    [Fact(Skip = Todo.H4_3)]
    public async Task TheLoop_ClaimsAtStartOnANudgeAndOnTheSweep()
    {
        _api.AddJob(Job(1));
        var loop = Loop();
        await loop.StartAsync(CancellationToken.None);

        try
        {
            await WaitUntil(() => _jobs.Started.Contains(1));

            // A nudge: no time passes on the fake clock, so only the nudge can wake it.
            _api.AddJob(Job(2));
            _signal.Nudge();
            await WaitUntil(() => _jobs.Started.Contains(2));

            // No nudge: only the sweep finds it.
            _api.AddJob(Job(3));
            await WaitUntil(() =>
            {
                _time.Advance(_options.Sweep);
                return _jobs.Started.Contains(3);
            });
        }
        finally
        {
            await loop.StopAsync(CancellationToken.None);
        }
    }

    [Fact(Skip = Todo.H4_3)]
    public async Task TheLoop_SurvivesTheApiBeingDown()
    {
        _api.ClaimFailures.Enqueue(new HttpRequestException("Connection refused"));
        _api.AddJob(Job(1));
        var loop = Loop();
        await loop.StartAsync(CancellationToken.None);

        try
        {
            await WaitUntil(() =>
            {
                _time.Advance(_options.Sweep);
                return _jobs.Started.Contains(1);
            });
        }
        finally
        {
            await loop.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the job loop.");
            await Task.Delay(10);
        }
    }
}
