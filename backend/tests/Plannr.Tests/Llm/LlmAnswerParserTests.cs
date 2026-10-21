using System.Text.Json;
using Plannr.Infrastructure.Llm;

namespace Plannr.Tests.Llm;

public sealed class LlmAnswerParserTests
{
    [Fact]
    public void TheAskedForShape_IsReadField_ByField()
    {
        var fields = LlmAnswerParser.Parse("""
            {
              "title": { "value": "Thrift Market", "confidence": 0.95 },
              "startsAt": { "value": "2026-12-14T10:00", "confidence": 0.9 },
              "price": { "value": "Free", "confidence": 0.8 },
              "address": null
            }
            """);

        Assert.Equal(new("Thrift Market", 0.95), fields.Title);
        Assert.Equal(new("2026-12-14T10:00", 0.9), fields.StartsAt);
        Assert.Equal("Free", fields.Price!.Value);
        Assert.Null(fields.Address);
        Assert.Null(fields.Category);
    }

    [Fact]
    public void ABareValue_GetsTheDefaultConfidence()
    {
        var fields = LlmAnswerParser.Parse("""{ "title": "Thrift Market" }""");

        Assert.Equal(new("Thrift Market", LlmAnswerParser.DefaultConfidence), fields.Title);
    }

    [Fact]
    public void SnakeCaseNames_AreTheSameFields()
    {
        var fields = LlmAnswerParser.Parse("""{ "starts_at": { "value": "2026-12-14" }, "location_name": "Hall" }""");

        Assert.Equal("2026-12-14", fields.StartsAt!.Value);
        Assert.Equal("Hall", fields.LocationName!.Value);
    }

    [Fact]
    public void AnObjectWithANullValue_MeansAbsent()
    {
        Assert.Null(LlmAnswerParser.Parse("""{ "price": { "value": null, "confidence": 0.9 } }""").Price);
    }

    [Theory]
    [InlineData("""{ "price": { "value": 5, "confidence": "0.7" } }""", "5", 0.7)]
    [InlineData("""{ "price": { "value": "£5", "confidence": 1.4 } }""", "£5", 1.0)]
    [InlineData("""{ "price": { "value": "£5", "confidence": -2 } }""", "£5", 0.0)]
    public void NumbersAndOutOfRangeConfidences_AreTidied(string json, string value, double confidence)
    {
        Assert.Equal(new(value, confidence), LlmAnswerParser.Parse(json).Price);
    }

    [Fact]
    public void AFencedAnswer_IsStillRead()
    {
        var fields = LlmAnswerParser.Parse("```json\n{ \"title\": \"Market\" }\n```");

        Assert.Equal("Market", fields.Title!.Value);
    }

    [Fact]
    public void NotJson_Throws()
    {
        Assert.ThrowsAny<JsonException>(() => LlmAnswerParser.Parse("Sorry, I can't help with that."));
    }

    [Fact]
    public void JsonThatIsNotAnObject_Throws()
    {
        Assert.Throws<FormatException>(() => LlmAnswerParser.Parse("""["Thrift Market"]"""));
    }
}
