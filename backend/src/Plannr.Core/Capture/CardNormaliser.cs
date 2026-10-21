using Plannr.Core.Options;

namespace Plannr.Core.Capture;

/// <summary>
/// Merges what was read about one item into a <see cref="CardDraft"/>, field
/// by field, from the most trustworthy source available:
///   1. the page's own structured data (JSON-LD, mapped by code);
///   2. the LLM's answer, flagged Uncertain below the confidence threshold;
///   3. for title and description only, the page's title/description tags.
///
/// Flags are what the user is asked to check. Missing is only raised for
/// the fields a plan needs -- title, start, place, price -- not for
/// description, end, address or category, which many real activities don't
/// have. One flag per field at most, the first reason found.
/// </summary>
public sealed class CardNormaliser(ExtractionOptions options, TimeProvider time)
{
    private const int MaxLocationChars = 200;
    private const int MaxAddressChars = 300;

    public CardDraft Normalise(RetrievedContent content, StructuredFields? structured, LlmCardFields? llm)
    {
        var card = new Builder(options.FlagBelowConfidence);

        var title = card.Text(CardField.Title, structured?.Title, llm?.Title, content.Title, options.MaxTitleChars, required: true);
        var description = card.Text(CardField.Description, structured?.Description, llm?.Description, content.Description, options.MaxDescriptionChars, required: false);
        var location = card.Text(CardField.LocationName, structured?.LocationName, llm?.LocationName, null, MaxLocationChars, required: true);
        var address = card.Text(CardField.Address, structured?.Address, llm?.Address, null, MaxAddressChars, required: false);

        var (startsAt, startTimeKnown) = card.Date(
            CardField.StartsAt, structured?.StartsAt, structured?.StartTimeKnown ?? false, llm?.StartsAt, required: true);
        var (endsAt, endTimeKnown) = card.Date(
            CardField.EndsAt, structured?.EndsAt, structured?.EndTimeKnown ?? false, llm?.EndsAt, required: false);

        if (startsAt is not null && startsAt < StartOfTodayInUk())
        {
            // An expired listing, or a year the LLM got wrong: either way,
            // not something to plan for without checking.
            card.Flag(CardField.StartsAt, FlagReason.Uncertain);
        }

        if (startsAt is not null && endsAt is not null && endsAt < startsAt)
        {
            card.Flag(CardField.EndsAt, FlagReason.Invalid);
            card.Forget(CardField.EndsAt);
            (endsAt, endTimeKnown) = (null, false);
        }

        var (minPence, maxPence, currency) = card.Price(structured, llm?.Price, options.MaxPricePence);

        return new CardDraft
        {
            SourceUrl = content.SourceUrl,
            SourceType = content.SourceType,
            HasEventData = content.HasEventData,
            FetchedWith = content.FetchedWith,
            UsedLlm = llm is not null,
            Title = title,
            Description = description,
            StartsAt = startsAt,
            StartTimeKnown = startTimeKnown,
            EndsAt = endsAt,
            EndTimeKnown = endTimeKnown,
            LocationName = location,
            Address = address,
            PriceMinPence = minPence,
            PriceMaxPence = maxPence,
            Currency = currency,
            Category = card.Category(llm?.Category),
            ThumbnailUrl = content.ThumbnailUrl,
            Flags = card.Flags,
            FieldSources = card.Sources
        };
    }

    private DateTimeOffset StartOfTodayInUk() => UkTime.FromLocal(UkTime.Now(time).Date);

    /// <summary>Collects the flags and sources while each field is decided.</summary>
    private sealed class Builder(double flagBelowConfidence)
    {
        private readonly Dictionary<CardField, FieldFlag> _flags = [];
        private readonly Dictionary<CardField, FieldSource> _sources = [];

        public IReadOnlyList<FieldFlag> Flags => _flags.Values.OrderBy(f => f.Field).ToList();

        public IReadOnlyDictionary<CardField, FieldSource> Sources => _sources;

        public void Flag(CardField field, FlagReason reason, double? confidence = null) =>
            _flags.TryAdd(field, new FieldFlag(field, reason, confidence));

        public void Forget(CardField field) => _sources.Remove(field);

        public string? Text(CardField field, string? fromStructured, LlmField? fromLlm, string? fromPage, int maxChars, bool required)
        {
            if (Clean(fromStructured, maxChars) is { } structured)
            {
                _sources[field] = FieldSource.StructuredData;
                return structured;
            }

            if (Clean(fromLlm?.Value, maxChars) is { } llm)
            {
                _sources[field] = FieldSource.Llm;
                CheckConfidence(field, fromLlm!.Confidence);
                return llm;
            }

            if (Clean(fromPage, maxChars) is { } page)
            {
                // The page's own <title> is often "Event name | Venue | Tickets":
                // usable, but worth a look when it's standing in for a title.
                _sources[field] = FieldSource.PageMetadata;
                if (required)
                {
                    Flag(field, FlagReason.Uncertain);
                }

                return page;
            }

            if (required)
            {
                Flag(field, FlagReason.Missing);
            }

            return null;
        }

        /// <summary>
        /// A structured date with its time wins. A structured date with no
        /// time is improved by the LLM's answer if that has a time on the same
        /// day (the page text often says "doors 7pm" when the JSON-LD doesn't).
        /// </summary>
        public (DateTimeOffset? Value, bool TimeKnown) Date(
            CardField field, DateTimeOffset? structured, bool structuredHasTime, LlmField? fromLlm, bool required)
        {
            DateTimeOffset llmValue = default;
            var llmHasTime = false;
            var llmParsed = fromLlm is not null && UkDateParser.TryParse(fromLlm.Value, out llmValue, out llmHasTime);

            if (structured is { } fromPage && structuredHasTime)
            {
                _sources[field] = FieldSource.StructuredData;
                return (fromPage, true);
            }

            if (structured is { } day)
            {
                if (llmParsed && llmHasTime && SameUkDay(llmValue, day))
                {
                    _sources[field] = FieldSource.Llm;
                    CheckConfidence(field, fromLlm!.Confidence);
                    return (llmValue, true);
                }

                _sources[field] = FieldSource.StructuredData;
                return (day, false);
            }

            if (llmParsed)
            {
                _sources[field] = FieldSource.Llm;
                CheckConfidence(field, fromLlm!.Confidence);
                return (llmValue, llmHasTime);
            }

            if (fromLlm is not null)
            {
                // The model answered, but not in a shape we can trust as a date.
                Flag(field, FlagReason.Invalid, fromLlm.Confidence);
            }
            else if (required)
            {
                Flag(field, FlagReason.Missing);
            }

            return (null, false);
        }

        public (int? Min, int? Max, string? Currency) Price(StructuredFields? structured, LlmField? fromLlm, int maxPence)
        {
            if (structured?.PriceMinPence is { } structuredMin)
            {
                _sources[CardField.Price] = FieldSource.StructuredData;
                return (structuredMin, structured.PriceMaxPence, structured.Currency);
            }

            if (fromLlm is null)
            {
                Flag(CardField.Price, FlagReason.Missing);
                return (null, null, null);
            }

            if (!PriceParser.TryParse(fromLlm.Value, out var price) || price.MinPence > maxPence)
            {
                Flag(CardField.Price, FlagReason.Invalid, fromLlm.Confidence);
                return (null, null, null);
            }

            _sources[CardField.Price] = FieldSource.Llm;
            CheckConfidence(CardField.Price, fromLlm.Confidence);
            if (price.CurrencyAssumed)
            {
                Flag(CardField.Price, FlagReason.Uncertain, fromLlm.Confidence);
            }

            return (price.MinPence, price.MaxPence > maxPence ? null : price.MaxPence, price.Currency);
        }

        /// <summary>No flags: a category is a filter label, not something a plan depends on.</summary>
        public string? Category(LlmField? fromLlm)
        {
            var value = fromLlm?.Value?.Trim().ToLowerInvariant();
            if (value is null)
            {
                return null;
            }

            _sources[CardField.Category] = FieldSource.Llm;
            return ActivityCategories.IsKnown(value) ? value : ActivityCategories.Other;
        }

        private void CheckConfidence(CardField field, double confidence)
        {
            if (confidence < flagBelowConfidence)
            {
                Flag(field, FlagReason.Uncertain, confidence);
            }
        }

        private static bool SameUkDay(DateTimeOffset a, DateTimeOffset b) =>
            TimeZoneInfo.ConvertTime(a, UkTime.Zone).Date == TimeZoneInfo.ConvertTime(b, UkTime.Zone).Date;

        private static string? Clean(string? text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            text = text.Trim();
            return text.Length <= maxChars ? text : text[..maxChars].TrimEnd() + "...";
        }
    }
}
