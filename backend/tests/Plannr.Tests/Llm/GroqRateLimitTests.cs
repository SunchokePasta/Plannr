using System.Net.Http.Headers;
using Plannr.Infrastructure.Llm;

namespace Plannr.Tests.Llm;

public sealed class GroqRateLimitTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RetryAfterSeconds_ComesFirst()
    {
        var limit = GroqRateLimit.Read(new RetryConditionHeaderValue(TimeSpan.FromSeconds(7)), "try again in 9m", Now);

        Assert.Equal(TimeSpan.FromSeconds(7), limit.Wait);
    }

    [Fact]
    public void RetryAfterAsADate_IsTheTimeUntilThen()
    {
        var limit = GroqRateLimit.Read(new RetryConditionHeaderValue(Now.AddSeconds(42)), null, Now);

        Assert.Equal(TimeSpan.FromSeconds(42), limit.Wait);
    }

    [Theory]
    [InlineData("Please try again in 7.5s.", 7.5)]
    [InlineData("Please try again in 1m30.5s.", 90.5)]
    [InlineData("Please try again in 3h12m5s.", 11525)]
    [InlineData("Please try again in 520ms.", 0.52)]
    [InlineData("please TRY AGAIN IN 2m", 120)]
    public void WithoutAHeader_TheWaitInTheMessageIsUsed(string message, double seconds)
    {
        var limit = GroqRateLimit.Read(null, $$$"""{"error":{"message":"{{{message}}}"}}""", Now);

        Assert.Equal(seconds, limit.Wait!.Value.TotalSeconds, precision: 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("""{"error":"slow down"}""")]
    public void NoWaitAnywhere_IsUnknown(string? body)
    {
        Assert.Null(GroqRateLimit.Read(null, body, Now).Wait);
    }

    [Theory]
    [InlineData("on tokens per day (TPD): Limit 500000", true)]
    [InlineData("on requests per day (RPD): Limit 1000", true)]
    [InlineData("on tokens per minute (TPM): Limit 6000", false)]
    [InlineData("on requests per minute (RPM): Limit 30", false)]
    public void TheDailyLimit_IsToldApartFromThePerMinuteOne(string body, bool daily)
    {
        Assert.Equal(daily, GroqRateLimit.Read(null, body, Now).IsDaily);
    }

    [Theory]
    [InlineData(7.5, "7.5s")]
    [InlineData(90, "1m 30s")]
    [InlineData(11525, "3h 12m")]
    public void Describe_IsShortAndReadable(double seconds, string expected)
    {
        Assert.Equal(expected, GroqRateLimit.Describe(TimeSpan.FromSeconds(seconds)));
    }
}
