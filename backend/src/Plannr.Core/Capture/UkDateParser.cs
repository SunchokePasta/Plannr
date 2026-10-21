using System.Globalization;
using System.Text.RegularExpressions;

namespace Plannr.Core.Capture;

/// <summary>
/// Reads the ISO 8601 shapes dates arrive in, from JSON-LD and from the
/// LLM (which is asked for yyyy-MM-ddTHH:mm). A time with an offset is
/// exact; one without is UK wall-clock time; a bare date is a day with no
/// time. Anything else is refused rather than guessed at -- free text like
/// "Sat 14th" is the LLM's job, not this parser's.
/// </summary>
public static partial class UkDateParser
{
    private static readonly string[] LocalFormats =
    [
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm"
    ];

    /// <param name="hasTime">False for a bare date: <paramref name="value"/> is then midnight UK time.</param>
    public static bool TryParse(string? text, out DateTimeOffset value, out bool hasTime)
    {
        value = default;
        hasTime = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();

        if (HasOffset().IsMatch(text))
        {
            if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            {
                return false;
            }

            value = TimeZoneInfo.ConvertTime(exact, UkTime.Zone);
            hasTime = true;
            return true;
        }

        if (DateTime.TryParseExact(text, LocalFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            value = UkTime.FromLocal(local);
            hasTime = true;
            return true;
        }

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            value = UkTime.FromLocal(day);
            return true;
        }

        return false;
    }

    /// <summary>A date, a time, then Z or ±hh:mm / ±hhmm.</summary>
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T[\d:.]+(Z|[+-]\d{2}:?\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex HasOffset();
}
