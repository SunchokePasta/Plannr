using Plannr.Core.Capture;

namespace Plannr.Tests.Capture;

public sealed class UkDateParserTests
{
    [Theory]
    [InlineData("2026-12-14T19:30:00+00:00")]
    [InlineData("2026-12-14T19:30:00Z")]
    [InlineData("2026-12-14T19:30:00.000Z")]
    [InlineData("2026-12-14T14:30:00-05:00")]
    [InlineData("2026-12-14T19:30:00+0000")]
    public void WithAnOffset_IsExactAndShownInUkTime(string text)
    {
        Assert.True(UkDateParser.TryParse(text, out var value, out var hasTime));

        Assert.True(hasTime);
        Assert.Equal(new DateTimeOffset(2026, 12, 14, 19, 30, 0, TimeSpan.Zero), value);
        Assert.Equal(TimeSpan.Zero, value.Offset);
    }

    [Theory]
    [InlineData("2026-07-04T19:30")]
    [InlineData("2026-07-04T19:30:00")]
    [InlineData("2026-07-04 19:30")]
    public void WithoutAnOffset_IsUkWallClockTime(string text)
    {
        Assert.True(UkDateParser.TryParse(text, out var value, out var hasTime));

        Assert.True(hasTime);
        Assert.Equal(new DateTimeOffset(2026, 7, 4, 19, 30, 0, TimeSpan.FromHours(1)), value);
    }

    [Fact]
    public void ABareDate_IsMidnightWithNoTime()
    {
        Assert.True(UkDateParser.TryParse("2026-12-14", out var value, out var hasTime));

        Assert.False(hasTime);
        Assert.Equal(new DateTimeOffset(2026, 12, 14, 0, 0, 0, TimeSpan.Zero), value);
    }

    [Theory]
    [InlineData("Sat 14th Dec")]
    [InlineData("14/12/2026")]
    [InlineData("2026-13-01")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElse_IsRefused(string? text)
    {
        Assert.False(UkDateParser.TryParse(text, out _, out _));
    }
}
