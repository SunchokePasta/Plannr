using Microsoft.Extensions.Time.Testing;
using Plannr.Core.Capture;
using Plannr.Core.Options;

namespace Plannr.Tests.Capture;

public sealed class CardNormaliserTests
{
    private static readonly RetrievedContent Page = new()
    {
        SourceUrl = new Uri("https://venue.example.com/e/1"),
        SourceType = SourceType.WebPage,
        FetchedWith = FetchMethod.Http,
        Title = "Thrift Market | Venue | Tickets",
        Description = "Vintage stalls all day."
    };

    private static readonly LlmCardFields ConfidentLlm = new()
    {
        Title = new("Thrift Market", 0.95),
        StartsAt = new("2026-12-14T10:00", 0.9),
        LocationName = new("Old Hall", 0.9),
        Price = new("Free", 0.9),
        Category = new("markets", 0.9)
    };

    private readonly CardNormaliser _normaliser = new(
        new ExtractionOptions(),
        new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 17, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void AConfidentLlmAnswer_GivesACardWithNoFlags()
    {
        var card = _normaliser.Normalise(Page, structured: null, ConfidentLlm);

        Assert.Equal("Thrift Market", card.Title);
        Assert.Equal(new DateTimeOffset(2026, 12, 14, 10, 0, 0, TimeSpan.Zero), card.StartsAt);
        Assert.True(card.StartTimeKnown);
        Assert.Equal(0, card.PriceMinPence);
        Assert.Equal("markets", card.Category);
        Assert.Empty(card.Flags);
        Assert.True(card.UsedLlm);
        Assert.Equal(FieldSource.Llm, card.FieldSources[CardField.Title]);
    }

    [Fact]
    public void StructuredData_WinsOverTheLlm()
    {
        var structured = new StructuredFields
        {
            Title = "Thrift Market (JSON-LD)",
            StartsAt = new DateTimeOffset(2026, 12, 14, 11, 0, 0, TimeSpan.Zero),
            StartTimeKnown = true,
            PriceMinPence = 300,
            PriceMaxPence = 300,
            Currency = "GBP"
        };

        var card = _normaliser.Normalise(Page, structured, ConfidentLlm);

        Assert.Equal("Thrift Market (JSON-LD)", card.Title);
        Assert.Equal(11, card.StartsAt!.Value.Hour);
        Assert.Equal(300, card.PriceMinPence);
        Assert.Equal(FieldSource.StructuredData, card.FieldSources[CardField.Title]);
        Assert.Equal(FieldSource.Llm, card.FieldSources[CardField.LocationName]);
    }

    [Fact]
    public void AStructuredDateWithNoTime_TakesTheLlmsTimeOnTheSameDay()
    {
        var structured = new StructuredFields { StartsAt = new DateTimeOffset(2026, 12, 14, 0, 0, 0, TimeSpan.Zero), StartTimeKnown = false };

        var card = _normaliser.Normalise(Page, structured, ConfidentLlm);

        Assert.Equal(10, card.StartsAt!.Value.Hour);
        Assert.True(card.StartTimeKnown);
    }

    [Fact]
    public void AStructuredDateWithNoTime_IgnoresAnLlmTimeOnAnotherDay()
    {
        var structured = new StructuredFields { StartsAt = new DateTimeOffset(2026, 12, 15, 0, 0, 0, TimeSpan.Zero), StartTimeKnown = false };

        var card = _normaliser.Normalise(Page, structured, ConfidentLlm);

        Assert.Equal(15, card.StartsAt!.Value.Day);
        Assert.False(card.StartTimeKnown);
    }

    [Fact]
    public void LowConfidence_IsFlaggedUncertainWithTheConfidence()
    {
        var card = _normaliser.Normalise(Page, null, ConfidentLlm with { LocationName = new("Old Hall?", 0.4) });

        var flag = Assert.Single(card.Flags);
        Assert.Equal(new FieldFlag(CardField.LocationName, FlagReason.Uncertain, 0.4), flag);
        Assert.Equal("Old Hall?", card.LocationName);
    }

    [Fact]
    public void NothingFound_FlagsOnlyTheFieldsAPlanNeeds()
    {
        var card = _normaliser.Normalise(Page with { Title = null, Description = null }, null, new LlmCardFields());

        Assert.Equal(
            [CardField.Title, CardField.StartsAt, CardField.LocationName, CardField.Price],
            card.Flags.Select(f => f.Field));
        Assert.All(card.Flags, f => Assert.Equal(FlagReason.Missing, f.Reason));
    }

    [Fact]
    public void WithoutAnLlmTitle_ThePageTitleIsUsedAndFlagged()
    {
        var card = _normaliser.Normalise(Page, null, ConfidentLlm with { Title = null });

        Assert.Equal("Thrift Market | Venue | Tickets", card.Title);
        Assert.Equal(FieldSource.PageMetadata, card.FieldSources[CardField.Title]);
        Assert.Contains(new FieldFlag(CardField.Title, FlagReason.Uncertain), card.Flags);
    }

    [Fact]
    public void AnUnreadableDate_IsFlaggedInvalid()
    {
        var card = _normaliser.Normalise(Page, null, ConfidentLlm with { StartsAt = new("next Saturday-ish", 0.7) });

        Assert.Null(card.StartsAt);
        Assert.Contains(new FieldFlag(CardField.StartsAt, FlagReason.Invalid, 0.7), card.Flags);
    }

    [Fact]
    public void ADateThatHasPassed_IsFlaggedUncertain()
    {
        var card = _normaliser.Normalise(Page, null, ConfidentLlm with { StartsAt = new("2025-12-14T10:00", 0.9) });

        Assert.Contains(new FieldFlag(CardField.StartsAt, FlagReason.Uncertain), card.Flags);
    }

    [Fact]
    public void AnEndBeforeTheStart_IsDroppedAndFlagged()
    {
        var card = _normaliser.Normalise(Page, null, ConfidentLlm with { EndsAt = new("2026-12-14T09:00", 0.9) });

        Assert.Null(card.EndsAt);
        Assert.Contains(new FieldFlag(CardField.EndsAt, FlagReason.Invalid), card.Flags);
        Assert.False(card.FieldSources.ContainsKey(CardField.EndsAt));
    }

    [Fact]
    public void APriceWithNoCurrency_IsFlaggedUncertain()
    {
        var card = _normaliser.Normalise(Page, null, ConfidentLlm with { Price = new("10", 0.9) });

        Assert.Equal(1000, card.PriceMinPence);
        Assert.Contains(card.Flags, f => f is { Field: CardField.Price, Reason: FlagReason.Uncertain });
    }

    [Theory]
    [InlineData("See website")]
    [InlineData("£25,000")]
    public void AnUnusablePrice_IsFlaggedInvalid(string price)
    {
        var card = _normaliser.Normalise(Page, null, ConfidentLlm with { Price = new(price, 0.8) });

        Assert.Null(card.PriceMinPence);
        Assert.Contains(card.Flags, f => f is { Field: CardField.Price, Reason: FlagReason.Invalid });
    }

    [Fact]
    public void AnUnknownCategory_BecomesOther()
    {
        var card = _normaliser.Normalise(Page, null, ConfidentLlm with { Category = new("Retail Therapy", 0.9) });

        Assert.Equal(ActivityCategories.Other, card.Category);
    }

    [Fact]
    public void ALongTitle_IsCut()
    {
        var card = _normaliser.Normalise(Page, null, ConfidentLlm with { Title = new(new string('a', 300), 0.9) });

        Assert.Equal(123, card.Title!.Length);
        Assert.EndsWith("...", card.Title);
    }

    [Fact]
    public void WithoutTheLlm_StructuredDataAloneMakesTheCard()
    {
        var structured = new StructuredFields
        {
            Title = "Gig",
            StartsAt = new DateTimeOffset(2026, 12, 14, 19, 0, 0, TimeSpan.Zero),
            StartTimeKnown = true,
            LocationName = "Hall",
            PriceMinPence = 500,
            PriceMaxPence = 500,
            Currency = "GBP"
        };

        var card = _normaliser.Normalise(Page, structured, llm: null);

        Assert.False(card.UsedLlm);
        Assert.Empty(card.Flags);
        Assert.Null(card.Category);
    }
}
