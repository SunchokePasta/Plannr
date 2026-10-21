using System.Text.Json;
using Plannr.Core.Capture;
using Plannr.Core.Interfaces;
using Plannr.Eval;

namespace Plannr.Tests.Eval;

public sealed class ExtractCommandsTests : IDisposable
{
    private static readonly CardDraft Card = new()
    {
        SourceUrl = new Uri("https://venue.example.com/e/1"),
        SourceType = SourceType.WebPage,
        Title = "Thrift Market",
        PriceMinPence = 500,
        Currency = "GBP",
        Description = "Entry £5",
        Flags = [new FieldFlag(CardField.StartsAt, FlagReason.Missing)],
        FieldSources = new Dictionary<CardField, FieldSource> { [CardField.Title] = FieldSource.Llm }
    };

    private readonly string _imagePath = Path.Combine(Path.GetTempPath(), $"plannr-test-{Guid.NewGuid():N}.png");

    public void Dispose() => File.Delete(_imagePath);

    private sealed class FakePipeline : IExtractionPipeline
    {
        public (byte[] Bytes, string Declared)? Image { get; private set; }

        public Task<CardDraft> ExtractFromLinkAsync(Uri url, CancellationToken cancellationToken = default) =>
            Task.FromResult(Card);

        public Task<CardDraft> ExtractFromImageAsync(byte[] image, string declaredMediaType, CancellationToken cancellationToken = default)
        {
            Image = (image, declaredMediaType);
            return Task.FromResult(Card with { SourceUrl = null, SourceType = SourceType.Image });
        }
    }

    [Fact]
    public async Task Link_PrintsTheCardAsReadableJson()
    {
        var output = new StringWriter();

        var exit = await ExtractCommands.LinkAsync(new FakePipeline(), Card.SourceUrl!, output, CancellationToken.None);

        var json = output.ToString();
        Assert.Equal(0, exit);
        Assert.Contains("\"title\": \"Thrift Market\"", json);
        Assert.Contains("Entry £5", json);
        Assert.Contains("\"reason\": \"Missing\"", json);
        Assert.Contains("\"Title\": \"Llm\"", json);
        Assert.NotNull(JsonDocument.Parse(json));
    }

    [Fact]
    public async Task Image_ReadsTheFileAndPassesItsExtensionAsAHint()
    {
        await File.WriteAllBytesAsync(_imagePath, [1, 2, 3]);
        var pipeline = new FakePipeline();

        await ExtractCommands.ImageAsync(pipeline, _imagePath, new StringWriter(), CancellationToken.None);

        Assert.Equal([1, 2, 3], pipeline.Image!.Value.Bytes);
        Assert.Equal("image/png", pipeline.Image.Value.Declared);
    }
}
