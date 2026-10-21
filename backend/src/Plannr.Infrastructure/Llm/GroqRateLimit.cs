using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Plannr.Infrastructure.Llm;

/// <summary>What a 429 from Groq says about waiting.</summary>
/// <param name="Wait">How long Groq asked for, or null when it didn't say.</param>
/// <param name="IsDaily">The per-day limit (TPD/RPD), not the per-minute one.</param>
public sealed partial record GroqRateLimit(TimeSpan? Wait, bool IsDaily)
{
    /// <summary>
    /// Ported from Applr's JobAnalyser.groq_wait_seconds: the Retry-After
    /// header first, then the time in Groq's own message ("Please try again
    /// in 1m30.5s"), else unknown. Groq's message also names the limit
    /// ("tokens per day (TPD)"), which is how a daily limit is told apart.
    /// </summary>
    public static GroqRateLimit Read(RetryConditionHeaderValue? retryAfter, string? body, DateTimeOffset now)
    {
        var isDaily = body is not null && Daily().IsMatch(body);

        var fromHeader = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - now : (TimeSpan?)null);

        if (fromHeader is { } header && header > TimeSpan.Zero)
        {
            return new GroqRateLimit(header, isDaily);
        }

        return new GroqRateLimit(FromMessage(body), isDaily);
    }

    private static TimeSpan? FromMessage(string? body)
    {
        var match = body is null ? null : TryAgainIn().Match(body);
        if (match is not { Success: true })
        {
            return null;
        }

        var total = TimeSpan.Zero;
        foreach (Match part in DurationPart().Matches(match.Groups[1].Value))
        {
            var amount = double.Parse(part.Groups[1].Value, CultureInfo.InvariantCulture);
            total += part.Groups[2].Value switch
            {
                "h" => TimeSpan.FromHours(amount),
                "m" => TimeSpan.FromMinutes(amount),
                "ms" => TimeSpan.FromMilliseconds(amount),
                _ => TimeSpan.FromSeconds(amount)
            };
        }

        return total > TimeSpan.Zero ? total : null;
    }

    /// <summary>"3h 12m", "1m 30s", "7.5s" -- for log lines and error messages.</summary>
    public static string Describe(TimeSpan wait) =>
        wait.TotalHours >= 1 ? $"{(int)wait.TotalHours}h {wait.Minutes}m"
        : wait.TotalMinutes >= 1 ? $"{(int)wait.TotalMinutes}m {wait.Seconds}s"
        : $"{Math.Round(wait.TotalSeconds, 1).ToString(CultureInfo.InvariantCulture)}s";

    [GeneratedRegex(@"try again in\s+((?:\d+(?:\.\d+)?(?:ms|h|m|s)\s*)+)", RegexOptions.IgnoreCase)]
    private static partial Regex TryAgainIn();

    /// <summary>"ms" before "m" and "s", so "520ms" isn't read as 520 minutes and a stray s.</summary>
    [GeneratedRegex(@"(\d+(?:\.\d+)?)(ms|h|m|s)")]
    private static partial Regex DurationPart();

    [GeneratedRegex(@"per day|\(TPD\)|\(RPD\)", RegexOptions.IgnoreCase)]
    private static partial Regex Daily();
}
