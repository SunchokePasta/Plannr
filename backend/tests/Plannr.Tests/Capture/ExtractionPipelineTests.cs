using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Plannr.Core.Capture;
using Plannr.Core.Diagnostics;
using Plannr.Core.Exceptions;
using Plannr.Core.Interfaces;
using Plannr.Core.Options;
using Plannr.Tests.Diagnostics;

namespace Plannr.Tests.Capture;

/// <summary>The real classifier, mapper and normaliser; fake retriever and LLM.</summary>
[Collection(ErrorReferenceCollection.Name)]
public sealed class ExtractionPipelineTests : IDisposable
{
    private static readonly Uri EventUrl = new("https://venue.example.com/e/1");

    private const string CompleteJsonLd = """
        [{"@type":"Event","name":"Gig","startDate":"2026-12-14T19:00:00+00:00",
          "location":{"@type":"Place","name":"Hall"},"offers":{"price":"5","priceCurrency":"GBP"}}]
        """;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 17, 0, 0, TimeSpan.Zero));
    private readonly FakeLogger<ExtractionPipeline> _logger = new();

    public ExtractionPipelineTests() => ErrorReference.Configure(ErrorReference.EvalProgram, ErrorReference.MainLog);

    public void Dispose() => ErrorReference.Reset();

    private sealed class FakeRetriever(RetrievedContent content) : IContentRetriever
    {
        public SourceType Handles => SourceType.WebPage;

        public Task<RetrievedContent> RetrieveAsync(Uri url, CancellationToken cancellationToken = default) =>
            Task.FromResult(content);
    }

    private sealed class FakeLlm(Func<RetrievedContent, LlmCardFields> answer) : ILlmExtractor
    {
        public List<RetrievedContent> Calls { get; } = [];

        public Task<LlmCardFields> ExtractAsync(RetrievedContent content, CancellationToken cancellationToken = default)
        {
            Calls.Add(content);
            return Task.FromResult(answer(content));
        }
    }

    private static RetrievedContent Page(string? jsonLd = null, string? title = "Gig | Hall") => new()
    {
        SourceUrl = EventUrl,
        SourceType = SourceType.WebPage,
        FetchedWith = FetchMethod.Http,
        Title = title,
        StructuredData = jsonLd,
        HasEventData = jsonLd is not null
    };

    private static readonly LlmCardFields LlmAnswer = new()
    {
        Title = new("Gig", 0.9),
        StartsAt = new("2026-12-14T19:00", 0.9),
        LocationName = new("Hall", 0.9),
        Price = new("£5", 0.9),
        Category = new("music", 0.9)
    };

    private ExtractionPipeline Pipeline(RetrievedContent page, ILlmExtractor llm, ExtractionOptions? options = null)
    {
        options ??= new ExtractionOptions();
        return new ExtractionPipeline(
            new LinkClassifier(), [new FakeRetriever(page)], llm,
            new CardNormaliser(options, _time), options, _time, _logger);
    }

    [Fact]
    public async Task APageWithNoStructuredData_AsksTheLlm()
    {
        var llm = new FakeLlm(_ => LlmAnswer);

        var card = await Pipeline(Page(), llm).ExtractFromLinkAsync(EventUrl);

        Assert.Single(llm.Calls);
        Assert.True(card.UsedLlm);
        Assert.Equal("Gig", card.Title);
        Assert.Equal(500, card.PriceMinPence);
        Assert.Empty(card.Flags);
    }

    [Fact]
    public async Task CompleteJsonLd_SkipsTheLlm()
    {
        var llm = new FakeLlm(_ => LlmAnswer);

        var card = await Pipeline(Page(CompleteJsonLd), llm).ExtractFromLinkAsync(EventUrl);

        Assert.Empty(llm.Calls);
        Assert.False(card.UsedLlm);
        Assert.Equal("Hall", card.LocationName);
        Assert.All(card.FieldSources.Values, source => Assert.Equal(FieldSource.StructuredData, source));
    }

    [Fact]
    public async Task CompleteJsonLd_StillAsksTheLlmWhenSkippingIsOff()
    {
        var llm = new FakeLlm(_ => LlmAnswer);

        await Pipeline(Page(CompleteJsonLd), llm, new ExtractionOptions { SkipLlmWhenStructuredComplete = false })
            .ExtractFromLinkAsync(EventUrl);

        Assert.Single(llm.Calls);
    }

    [Fact]
    public async Task ALinkTypeWithNoRetrieverYet_IsUnsupported()
    {
        var exception = await Assert.ThrowsAsync<UnsupportedLinkException>(() =>
            Pipeline(Page(), new FakeLlm(_ => LlmAnswer)).ExtractFromLinkAsync(new Uri("https://www.tiktok.com/@a/video/1")));

        Assert.Contains("TikTok", exception.Message);
    }

    [Fact]
    public async Task AnInstagramLink_IsRefusedBeforeAnythingIsFetched()
    {
        await Assert.ThrowsAsync<InstagramNotSupportedException>(() =>
            Pipeline(Page(), new FakeLlm(_ => LlmAnswer)).ExtractFromLinkAsync(new Uri("https://instagram.com/p/x")));
    }

    [Fact]
    public async Task AnLlmFailure_WithUsablePageData_GivesAPartialCardAndAWarning()
    {
        var llm = new FakeLlm(_ => throw new ExtractionFailedException("Groq returned 503"));

        var card = await Pipeline(Page(), llm).ExtractFromLinkAsync(EventUrl);

        Assert.Equal("Gig | Hall", card.Title);
        Assert.Contains(card.Flags, f => f is { Field: CardField.StartsAt, Reason: FlagReason.Missing });
        var warning = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Matches(@"^\[[0-9a-f]{8}:03:01\]", warning.Message);
    }

    [Fact]
    public async Task AnLlmFailure_WithNothingElse_Propagates()
    {
        var llm = new FakeLlm(_ => throw new ExtractionFailedException("Groq returned 503"));

        await Assert.ThrowsAsync<ExtractionFailedException>(() =>
            Pipeline(Page(title: null), llm).ExtractFromLinkAsync(EventUrl));
    }

    [Fact]
    public async Task AScreenshot_GoesToTheLlmWithItsDetectedType()
    {
        var llm = new FakeLlm(_ => LlmAnswer);

        var card = await Pipeline(Page(), llm).ExtractFromImageAsync(Png, "application/octet-stream");

        var sent = Assert.Single(llm.Calls);
        Assert.Equal("image/png", sent.ImageMediaType);
        Assert.Null(card.SourceUrl);
        Assert.Equal(SourceType.Image, card.SourceType);
    }

    [Fact]
    public async Task AScreenshotThatFails_Propagates()
    {
        var llm = new FakeLlm(_ => throw new ExtractionFailedException("vision model down"));

        await Assert.ThrowsAsync<ExtractionFailedException>(() => Pipeline(Page(), llm).ExtractFromImageAsync(Png, "image/png"));
    }

    [Fact]
    public async Task AFileThatIsNotAnImage_IsRefusedBeforeTheLlm()
    {
        var llm = new FakeLlm(_ => LlmAnswer);

        await Assert.ThrowsAsync<UnsupportedImageException>(() =>
            Pipeline(Page(), llm).ExtractFromImageAsync("%PDF-1.7"u8.ToArray(), "image/png"));

        Assert.Empty(llm.Calls);
    }

    [Fact]
    public async Task ATooLargeImage_IsRefused()
    {
        var big = new byte[200_001];
        Png.CopyTo(big, 0);

        await Assert.ThrowsAsync<UnsupportedImageException>(() =>
            Pipeline(Page(), new FakeLlm(_ => LlmAnswer), new ExtractionOptions { MaxImageBytes = 200_000 })
                .ExtractFromImageAsync(big, "image/png"));
    }

    [Fact]
    public async Task EachCapture_LogsOneSummaryLine()
    {
        await Pipeline(Page(), new FakeLlm(_ => LlmAnswer with { Price = null })).ExtractFromLinkAsync(EventUrl);

        var line = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, line.Level);
        Assert.Contains("1 flag(s) Price:Missing", line.Message);
    }
}
