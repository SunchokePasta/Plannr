using Plannr.Core.Capture;

namespace Plannr.Tests.Capture;

public sealed class PriceParserTests
{
    [Theory]
    [InlineData("£5", 500, 500, "GBP")]
    [InlineData("£12.50", 1250, 1250, "GBP")]
    [InlineData("£5-£10", 500, 1000, "GBP")]
    [InlineData("£5 – £10", 500, 1000, "GBP")]
    [InlineData("£10 to £5", 500, 1000, "GBP")]
    [InlineData("50p", 50, 50, "GBP")]
    [InlineData("€15", 1500, 1500, "EUR")]
    [InlineData("$20", 2000, 2000, "USD")]
    [InlineData("25 GBP", 2500, 2500, "GBP")]
    [InlineData("£1,200", 120000, 120000, "GBP")]
    [InlineData("2 for £10", 1000, 1000, "GBP")]
    [InlineData("Doors 7pm, £5 on the door", 500, 500, "GBP")]
    public void Prices_AreReadInPence(string text, int min, int max, string currency)
    {
        Assert.True(PriceParser.TryParse(text, out var price));

        Assert.Equal(new ParsedPrice(min, max, currency, CurrencyAssumed: false), price);
    }

    [Fact]
    public void From_GivesAFloorWithNoTop()
    {
        Assert.True(PriceParser.TryParse("From £12.50", out var price));

        Assert.Equal(1250, price.MinPence);
        Assert.Null(price.MaxPence);
    }

    [Theory]
    [InlineData("Free")]
    [InlineData("free entry")]
    [InlineData("£0")]
    [InlineData("£0.00")]
    public void Free_IsZero(string text)
    {
        Assert.True(PriceParser.TryParse(text, out var price));

        Assert.Equal(new ParsedPrice(0, 0, "GBP", CurrencyAssumed: false), price);
    }

    [Fact]
    public void FreeThenPaid_IsARangeFromZero()
    {
        Assert.True(PriceParser.TryParse("Free before 10pm, £5 after", out var price));

        Assert.Equal(0, price.MinPence);
        Assert.Equal(500, price.MaxPence);
    }

    [Fact]
    public void ANumberWithNoCurrency_AssumesPoundsAndSaysSo()
    {
        Assert.True(PriceParser.TryParse("10", out var price));

        Assert.Equal(1000, price.MinPence);
        Assert.True(price.CurrencyAssumed);
    }

    [Theory]
    [InlineData("Pay what you can")]
    [InlineData("See website")]
    [InlineData("")]
    [InlineData(null)]
    public void TextWithNoAmount_IsRefused(string? text)
    {
        Assert.False(PriceParser.TryParse(text, out _));
    }
}
