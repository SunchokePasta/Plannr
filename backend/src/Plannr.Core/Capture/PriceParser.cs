using System.Globalization;
using System.Text.RegularExpressions;

namespace Plannr.Core.Capture;

/// <summary>A price read from text, in whole pence.</summary>
/// <param name="CurrencyAssumed">No symbol or code was given, so GBP was assumed -- worth flagging.</param>
public sealed record ParsedPrice(int? MinPence, int? MaxPence, string Currency, bool CurrencyAssumed);

/// <summary>
/// Price text, as the LLM writes it ("£5", "£5-£10", "from £12.50", "Free",
/// "50p", "€15") or as sloppy JSON-LD has it ("£25.00"), into pence. Refuses
/// rather than guesses: text with no amount (and that isn't "free") fails.
///
/// Numbers without a currency are only used when there are no numbers
/// with one: in "2 for £10" or "Doors 7pm, £5", the 2 and the 7 aren't prices.
/// </summary>
public static partial class PriceParser
{
    public static bool TryParse(string? text, out ParsedPrice price)
    {
        price = new ParsedPrice(null, null, "GBP", true);

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();

        var amounts = Amounts(text);
        var priced = amounts.Where(a => a.Currency is not null).ToList();
        var used = priced.Count > 0 ? priced : amounts;

        if (StartsFree().IsMatch(text))
        {
            // "Free", or "Free before 10pm, £5 after": free at the bottom,
            // the paid price (if a real one is given) at the top.
            var top = priced.Count > 0 ? priced.Max(a => a.Pence) : 0;
            price = new ParsedPrice(0, top, priced.FirstOrDefault().Currency ?? "GBP", CurrencyAssumed: false);
            return true;
        }

        if (used.Count == 0)
        {
            return false;
        }

        var currency = used.FirstOrDefault(a => a.Currency is not null).Currency;
        var min = used.Min(a => a.Pence);

        // "From £12.50" gives a floor, not a range: the upper price is unknown.
        price = From().IsMatch(text)
            ? new ParsedPrice(min, null, currency ?? "GBP", currency is null)
            : new ParsedPrice(min, used.Max(a => a.Pence), currency ?? "GBP", currency is null);

        return true;
    }

    private static List<(int Pence, string? Currency)> Amounts(string text)
    {
        var amounts = new List<(int, string?)>();

        foreach (Match match in Amount().Matches(text))
        {
            var number = decimal.Parse(match.Groups["num"].Value.Replace(",", string.Empty), CultureInfo.InvariantCulture);
            var isPence = match.Groups["p"].Success;
            var pence = isPence ? (int)number : (int)Math.Round(number * 100, MidpointRounding.AwayFromZero);

            amounts.Add((pence, CurrencyOf(match.Groups["sym"].Value, match.Groups["code"].Value, isPence)));
        }

        return amounts;
    }

    private static string? CurrencyOf(string symbol, string code, bool isPence) => symbol switch
    {
        "£" => "GBP",
        "$" => "USD",
        "€" => "EUR",
        _ when code.Length == 3 => code.ToUpperInvariant(),
        _ when isPence => "GBP",
        _ => null
    };

    [GeneratedRegex(@"^(free\b|no charge\b|£\s*0(\.00)?$)", RegexOptions.IgnoreCase)]
    private static partial Regex StartsFree();

    [GeneratedRegex(@"^(from|starting at|starts at)\b", RegexOptions.IgnoreCase)]
    private static partial Regex From();

    /// <summary>
    /// An optional symbol, a number (thousands commas allowed, up to two
    /// decimals), then an optional "p" (pence) or ISO code. "7pm" is not
    /// 7 pence: the p must end the word.
    /// </summary>
    [GeneratedRegex(@"(?<sym>[£$€])?\s*(?<num>\d{1,3}(?:,\d{3})+(?:\.\d{1,2})?|\d+(?:\.\d{1,2})?)(?:(?<p>p)\b|\s*(?<code>GBP|USD|EUR)\b)?", RegexOptions.IgnoreCase)]
    private static partial Regex Amount();
}
