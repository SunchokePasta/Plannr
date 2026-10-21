using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Plannr.Core.Capture;
using Plannr.Infrastructure.Options;
using Plannr.Infrastructure.Retrieval;

namespace Plannr.Tests.Fixtures;

/// <summary>
/// Each fixture page through the parser and the JSON-LD mapper: what Plannr
/// reads from realistic pages before any LLM is involved.
/// </summary>
public sealed class FixtureReadingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 18, 0, 0, TimeSpan.FromHours(1));

    private static RetrievedContent Parse(string page) =>
        new HtmlContentParser(new RetrievalOptions(), NullLogger<HtmlContentParser>.Instance)
            .Parse(FixturePages.Read(page), FixturePages.UrlFor(page), SourceType.WebPage);

    private static StructuredFields Map(string page) =>
        JsonLdEventMapper.Map(Parse(page).StructuredData, Now)
        ?? throw new InvalidOperationException($"{page} gave no structured fields.");

    [Fact]
    public void Ticketing_KeepsOnlyTheEventOfItsFourBlocks()
    {
        var content = Parse(FixturePages.Ticketing);

        Assert.True(content.HasEventData);
        var kept = Assert.Single(JsonNode.Parse(content.StructuredData!)!.AsArray());
        Assert.Equal("BusinessEvent", kept!["@type"]!.GetValue<string>());
        Assert.Equal("Southbank Zine Fair", content.Title);
    }

    [Fact]
    public void Ticketing_BodyTextLeavesOutNavFooterAndScripts()
    {
        var body = Parse(FixturePages.Ticketing).BodyText!;

        Assert.Contains("Sixty independent makers", body);
        Assert.DoesNotContain("Log in", body);
        Assert.DoesNotContain("Privacy", body);
        Assert.DoesNotContain("__APP_STATE__", body);
    }

    [Fact]
    public void Ticketing_MapsToACompleteCard()
    {
        var fields = Map(FixturePages.Ticketing);

        Assert.Equal("Southbank Zine Fair", fields.Title);
        Assert.Equal(new DateTimeOffset(2027, 2, 13, 11, 0, 0, TimeSpan.Zero), fields.StartsAt);
        Assert.Equal("Riverside Hall", fields.LocationName);
        Assert.Equal("12 Example Walk, London, SE1 9ZZ", fields.Address);
        Assert.Equal((0, 850, "GBP"), (fields.PriceMinPence, fields.PriceMaxPence, fields.Currency));
        Assert.True(fields.IsComplete);
    }

    [Fact]
    public void MusicVenue_ReadsTheEventFromTheGraphInUkTime()
    {
        var fields = Map(FixturePages.MusicVenue);

        Assert.Equal("The Night Owls + support", fields.Title);
        Assert.Equal(new DateTimeOffset(2026, 11, 27, 19, 30, 0, TimeSpan.Zero), fields.StartsAt);
        Assert.Equal("1 Sound Street, Manchester, M1 1AA", fields.Address);
        Assert.Equal(1800, fields.PriceMinPence);
    }

    [Fact]
    public void WordPress_DecodesEntitiesAndStripsHtml()
    {
        var fields = Map(FixturePages.WordPress);

        Assert.Equal("Winter Craft Market – Maker’s Edition", fields.Title);
        Assert.Equal("Handmade gifts from forty local makers. Hot drinks & mince pies.", fields.Description);
        Assert.Equal(0, fields.PriceMinPence);
        Assert.False(fields.EndTimeKnown);
        Assert.Equal("4 Mill Lane, Leeds, LS1 4XX", fields.Address);
    }

    [Fact]
    public void WhatsOn_PicksTheFirstEventNotYetPast()
    {
        Assert.Equal("Late Laughs", Map(FixturePages.WhatsOn).Title);
    }

    [Fact]
    public void Restaurant_GivesTheLocationButNoEvent()
    {
        var content = Parse(FixturePages.Restaurant);
        var fields = Map(FixturePages.Restaurant);

        Assert.False(content.HasEventData);
        Assert.Equal("Example Noodle Bar", fields.LocationName);
        Assert.Equal("22 Harbour Road, Bristol, BS1 5AA", fields.Address);
        Assert.Null(fields.Title);
    }

    [Fact]
    public void Blog_HasOnlyTextForTheLlm()
    {
        var content = Parse(FixturePages.Blog);

        Assert.Null(content.StructuredData);
        Assert.Contains("candlelit ghost walk", content.BodyText);
        Assert.Equal("10 cosy things to do in York this winter", content.Title);
    }

    [Fact]
    public void JavaScriptShell_HasTooLittleTextToUse()
    {
        var content = Parse(FixturePages.JavaScriptShell);

        Assert.Null(content.StructuredData);
        Assert.True((content.BodyText?.Length ?? 0) < new RetrievalOptions().MinBodyTextChars);
    }

    [Fact]
    public void BrokenJsonLd_StillReadsTheValidBlock()
    {
        var fields = Map(FixturePages.BrokenJsonLd);

        Assert.Equal("Clay Studio", fields.LocationName);
        Assert.Equal(3500, fields.PriceMinPence);
    }
}
