using Microsoft.Extensions.Time.Testing;
using Plannr.Services.HandOffs;

namespace Plannr.Tests.HandOffs;

/// <summary>H1. A fake clock, so "the sweep interval passed" is a line of test code, not a real wait.</summary>
public sealed class WakeSignalTests
{
    private static readonly TimeSpan Sweep = TimeSpan.FromSeconds(45);

    private readonly FakeTimeProvider _time = new();

    [Fact(Skip = Todo.H1_3)]
    public async Task ANudgeBeforeTheWait_EndsItAtOnce()
    {
        var signal = new WakeSignal(_time);

        signal.Nudge();

        Assert.True(await signal.WaitAsync(Sweep, CancellationToken.None));
    }

    [Fact(Skip = Todo.H1_3)]
    public async Task ANudgeDuringTheWait_EndsIt()
    {
        var signal = new WakeSignal(_time);
        var waiting = signal.WaitAsync(Sweep, CancellationToken.None);
        Assert.False(waiting.IsCompleted);

        signal.Nudge();

        Assert.True(await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact(Skip = Todo.H1_3)]
    public async Task WithoutANudge_TheWaitEndsAfterTheSweepInterval()
    {
        var signal = new WakeSignal(_time);
        var waiting = signal.WaitAsync(Sweep, CancellationToken.None);

        _time.Advance(Sweep - TimeSpan.FromSeconds(1));
        Assert.False(waiting.IsCompleted);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.False(await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact(Skip = Todo.H1_3)]
    public async Task ManyNudges_WakeTheLoopOnce()
    {
        var signal = new WakeSignal(_time);

        signal.Nudge();
        signal.Nudge();
        signal.Nudge();

        Assert.True(await signal.WaitAsync(Sweep, CancellationToken.None));

        // The other two were the same news: the next wait is a normal one.
        var next = signal.WaitAsync(Sweep, CancellationToken.None);
        Assert.False(next.IsCompleted);
        _time.Advance(Sweep);
        Assert.False(await next.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact(Skip = Todo.H1_3)]
    public async Task Cancelling_StopsTheWait()
    {
        var signal = new WakeSignal(_time);
        using var cancel = new CancellationTokenSource();
        var waiting = signal.WaitAsync(Sweep, cancel.Token);

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
