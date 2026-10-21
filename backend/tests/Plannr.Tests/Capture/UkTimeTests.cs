using Plannr.Core.Capture;

namespace Plannr.Tests.Capture;

public sealed class UkTimeTests
{
    [Fact]
    public void Winter_IsGmt()
    {
        Assert.Equal(TimeSpan.Zero, UkTime.FromLocal(new DateTime(2026, 12, 14, 19, 0, 0)).Offset);
    }

    [Fact]
    public void Summer_IsBst()
    {
        Assert.Equal(TimeSpan.FromHours(1), UkTime.FromLocal(new DateTime(2026, 7, 4, 19, 0, 0)).Offset);
    }

    [Fact]
    public void ATimeTheClocksSkip_MovesAnHourLater()
    {
        // 29 March 2026: 01:00 GMT jumps to 02:00 BST, so 01:30 never happens.
        var moment = UkTime.FromLocal(new DateTime(2026, 3, 29, 1, 30, 0));

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 2, 30, 0, TimeSpan.FromHours(1)), moment);
    }

    [Fact]
    public void ATimeThatHappensTwice_IsTheFirstOne()
    {
        // 25 October 2026: 02:00 BST goes back to 01:00 GMT, so 01:30 happens twice.
        var moment = UkTime.FromLocal(new DateTime(2026, 10, 25, 1, 30, 0));

        Assert.Equal(TimeSpan.FromHours(1), moment.Offset);
    }

    [Fact]
    public void Now_IsLondonTime()
    {
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(13, UkTime.Now(time).Hour);
    }
}
