using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Plannr.Core.Capture;
using Plannr.Core.Interfaces;
using Plannr.Core.Options;
using Plannr.Infrastructure.Options;
using Plannr.Infrastructure.Retrieval;

namespace Plannr.Tests.Fixtures;

/// <summary>
/// The whole pipeline -- classifier, WebPageRetriever, parser, mapper,
/// normaliser -- over the fixture pages, with only the network and the LLM
/// faked. Shows, page type by page type, when the LLM is needed and which
/// fields come from where.
/// </summary>
public sealed class FixturePipelineTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 17, 0, 0, TimeSpan.Zero));

    /// <summary>Serves each fixture at its FixturePages.UrlFor address.</summary>
    private sealed class FixtureFetcher : IPageFetcher
    {
        public Task<FetchedPage> FetchAsync(Uri url, CancellationToken cancellationToken = default)
        {
            var name = url.AbsolutePath.TrimStart('/') + ".html";
            return Task.FromResult(new FetchedPage(url, 200, FixturePages.Read(name), FetchMethod.Http));
        }
    }

    private sealed class RecordingLlm(LlmCardFields answer) : ILlmExtractor
    {
        public List<RetrievedContent> Calls { get; } = [];

        public Task<LlmCardFields> ExtractAsync(RetrievedContent content, CancellationToken cancellationToken = default)
        {
            Calls.Add(content);
            return Task.FromResult(answer);
        }
    }

    private IExtractionPipeline Pipeline(ILlmExtractor llm)
    {
        var retrievalOptions = new RetrievalOptions();
        var extractionOptions = new ExtractionOptions();
        var retriever = new WebPageRetriever(
            new FixtureFetcher(),
            new HtmlContentParser(retrievalOptions, NullLogger<HtmlContentParser>.Instance),
            retrievalOptions,
            NullLogger<WebPageRetriever>.Instance,
            browserFetcher: null);

        return new ExtractionPipeline(
            new LinkClassifier(), [retriever], llm,
            new CardNormaliser(extractionOptions, _time), extractionOptions, _time,
            NullLogger<ExtractionPipeline>.Instance);
    }

    private Task<CardDraft> Extract(string page, RecordingLlm llm) =>
        Pipeline(llm).ExtractFromLinkAsync(FixturePages.UrlFor(page));

    [Theory]
    [InlineData(FixturePages.Ticketing)]
    [InlineData(FixturePages.MusicVenue)]
    [InlineData(FixturePages.WordPress)]
    [InlineData(FixturePages.BrokenJsonLd)]
    public async Task WellStructuredEventPages_NeedNoLlmAndNoFlags(string page)
    {
        var llm = new RecordingLlm(new LlmCardFields());

        var card = await Extract(page, llm);

        Assert.Empty(llm.Calls);
        Assert.False(card.UsedLlm);
        Assert.True(card.HasEventData);
        Assert.Empty(card.Flags);
        Assert.All(card.FieldSources.Values, source => Assert.Equal(FieldSource.StructuredData, source));
    }

    [Fact]
    public async Task WordPress_KeepsADateOnlyEndAsADay()
    {
        var card = await Extract(FixturePages.WordPress, new RecordingLlm(new LlmCardFields()));

        Assert.Equal(new DateTimeOffset(2026, 12, 6, 0, 0, 0, TimeSpan.Zero), card.EndsAt);
        Assert.False(card.EndTimeKnown);
        Assert.True(card.StartTimeKnown);
    }

    [Fact]
    public async Task ABlogPost_IsReadEntirelyByTheLlm()
    {
        var llm = new RecordingLlm(new LlmCardFields
        {
            Title = new("Candlelit ghost walk", 0.85),
            StartsAt = new("2026-11-06T19:30", 0.6),
            LocationName = new("York Minster steps", 0.9),
            Price = new("£12", 0.9),
            Category = new("outdoors", 0.7)
        });

        var card = await Extract(FixturePages.Blog, llm);

        var sent = Assert.Single(llm.Calls);
        Assert.Contains("candlelit ghost walk", sent.BodyText);
        Assert.Equal("Candlelit ghost walk", card.Title);
        Assert.Equal(1200, card.PriceMinPence);
        Assert.False(card.HasEventData);

        // The LLM gave no description, so the page's own og:description is
        // used -- which, on a listicle, describes the whole article rather
        // than the one activity picked (see docs/decisions.md, D26).
        Assert.Equal(FieldSource.PageMetadata, card.FieldSources[CardField.Description]);
        Assert.All(
            card.FieldSources.Where(s => s.Key != CardField.Description),
            source => Assert.Equal(FieldSource.Llm, source.Value));
    }

    [Fact]
    public async Task ARestaurant_TakesItsAddressFromJsonLdAndTheRestFromTheLlm()
    {
        var llm = new RecordingLlm(new LlmCardFields
        {
            Title = new("Hand-pulled noodles at Example Noodle Bar", 0.8),
            LocationName = new("Noodle Bar", 0.7),
            Price = new("from £11", 0.8),
            Category = new("food-drink", 0.9)
        });

        var card = await Extract(FixturePages.Restaurant, llm);

        Assert.Single(llm.Calls);
        Assert.Equal("Example Noodle Bar", card.LocationName);
        Assert.Equal(FieldSource.StructuredData, card.FieldSources[CardField.LocationName]);
        Assert.Equal(FieldSource.Llm, card.FieldSources[CardField.Title]);
        Assert.Equal((1100, (int?)null), (card.PriceMinPence, card.PriceMaxPence));

        // No date: a restaurant is somewhere to go, not an event -- flagged for the user.
        Assert.Contains(card.Flags, f => f is { Field: CardField.StartsAt, Reason: FlagReason.Missing });
    }

    [Fact]
    public async Task AJavaScriptShellWithNoBrowser_GivesAMostlyEmptyCardToFillIn()
    {
        var llm = new RecordingLlm(new LlmCardFields());

        var card = await Extract(FixturePages.JavaScriptShell, llm);

        Assert.Single(llm.Calls);
        Assert.Equal("Example Events", card.Title);
        Assert.Equal(FieldSource.PageMetadata, card.FieldSources[CardField.Title]);
        Assert.Equal(
            [CardField.Title, CardField.StartsAt, CardField.LocationName, CardField.Price],
            card.Flags.Select(f => f.Field));
    }
}
