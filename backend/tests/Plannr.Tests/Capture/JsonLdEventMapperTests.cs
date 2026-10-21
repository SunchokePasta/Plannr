using Plannr.Core.Capture;

namespace Plannr.Tests.Capture;

public sealed class JsonLdEventMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 18, 0, 0, TimeSpan.FromHours(1));

    private const string FullEvent = """
        [{
          "@type": "MusicEvent",
          "name": "Late Night Jazz &amp; Soul",
          "description": "<p>Two sets.<br>Bar open late.</p>",
          "startDate": "2026-12-14T19:30:00+00:00",
          "endDate": "2026-12-14T23:00:00+00:00",
          "location": {
            "@type": "Place",
            "name": "Ronnie Scott's",
            "address": { "@type": "PostalAddress", "streetAddress": "47 Frith St", "addressLocality": "London", "postalCode": "W1D 4HT" }
          },
          "offers": [
            { "@type": "Offer", "price": "25.00", "priceCurrency": "GBP" },
            { "@type": "Offer", "price": 40, "priceCurrency": "GBP" }
          ]
        }]
        """;

    [Fact]
    public void AFullEvent_MapsEveryField()
    {
        var fields = JsonLdEventMapper.Map(FullEvent, Now)!;

        Assert.Equal("Late Night Jazz & Soul", fields.Title);
        Assert.Equal("Two sets. Bar open late.", fields.Description);
        Assert.Equal(new DateTimeOffset(2026, 12, 14, 19, 30, 0, TimeSpan.Zero), fields.StartsAt);
        Assert.True(fields.StartTimeKnown);
        Assert.Equal(new DateTimeOffset(2026, 12, 14, 23, 0, 0, TimeSpan.Zero), fields.EndsAt);
        Assert.Equal("Ronnie Scott's", fields.LocationName);
        Assert.Equal("47 Frith St, London, W1D 4HT", fields.Address);
        Assert.Equal(2500, fields.PriceMinPence);
        Assert.Equal(4000, fields.PriceMaxPence);
        Assert.Equal("GBP", fields.Currency);
        Assert.True(fields.IsComplete);
    }

    [Fact]
    public void AnAggregateOffer_GivesTheRange()
    {
        var fields = JsonLdEventMapper.Map("""
            [{"@type":"Event","name":"Gig","offers":{"@type":"AggregateOffer","lowPrice":"12.5","highPrice":"30","priceCurrency":"gbp"}}]
            """, Now)!;

        Assert.Equal(1250, fields.PriceMinPence);
        Assert.Equal(3000, fields.PriceMaxPence);
        Assert.Equal("GBP", fields.Currency);
    }

    [Fact]
    public void APriceWrittenAsText_IsLeftForTheLlm()
    {
        var fields = JsonLdEventMapper.Map("""
            [{"@type":"Event","name":"Gig","offers":{"price":"£5 - £10","priceCurrency":"GBP"}}]
            """, Now)!;

        Assert.Null(fields.PriceMinPence);
        Assert.False(fields.IsComplete);
    }

    [Fact]
    public void APriceWithNoCurrency_IsLeftForTheLlm()
    {
        var fields = JsonLdEventMapper.Map("""[{"@type":"Event","name":"Gig","offers":{"price":"5"}}]""", Now)!;

        Assert.Null(fields.PriceMinPence);
    }

    [Fact]
    public void ADateWithNoTime_IsKnownToHaveNoTime()
    {
        var fields = JsonLdEventMapper.Map("""[{"@type":"Event","name":"Fair","startDate":"2026-12-14"}]""", Now)!;

        Assert.False(fields.StartTimeKnown);
        Assert.False(fields.IsComplete);
    }

    [Fact]
    public void OfSeveralEvents_TheFirstNotYetPastIsChosen()
    {
        var fields = JsonLdEventMapper.Map("""
            [
              {"@type":"Event","name":"Last month","startDate":"2026-09-01T19:00"},
              {"@type":"Event","name":"Tonight","startDate":"2026-10-21T20:00"},
              {"@type":"Event","name":"Next week","startDate":"2026-10-28T20:00"}
            ]
            """, Now)!;

        Assert.Equal("Tonight", fields.Title);
    }

    [Fact]
    public void AnOnlineEvent_IsOnline()
    {
        var fields = JsonLdEventMapper.Map("""
            [{"@type":"Event","name":"Webinar","location":{"@type":"VirtualLocation","url":"https://zoom.example"}}]
            """, Now)!;

        Assert.Equal("Online", fields.LocationName);
    }

    [Fact]
    public void APlaceOnItsOwn_GivesTheLocationOnly()
    {
        var fields = JsonLdEventMapper.Map("""
            [{"@type":"Restaurant","name":"Dishoom","address":"7 Boundary St, London E2 7JE"}]
            """, Now)!;

        Assert.Null(fields.Title);
        Assert.Equal("Dishoom", fields.LocationName);
        Assert.Equal("7 Boundary St, London E2 7JE", fields.Address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void NothingUsable_IsNull(string? json)
    {
        Assert.Null(JsonLdEventMapper.Map(json, Now));
    }
}
