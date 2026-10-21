using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Plannr.Core.Capture;
using Plannr.Core.Exceptions;
using Plannr.Infrastructure.Llm;
using Plannr.Infrastructure.Options;
using Refit;

namespace Plannr.Tests.Llm;

public sealed class GroqLlmExtractorTests
{
    private const string GoodAnswer = """{ "title": { "value": "Thrift Market", "confidence": 0.95 } }""";

    private static readonly RetrievedContent WebPage = new()
    {
        SourceUrl = new Uri("https://venue.example.com/e/1"),
        SourceType = SourceType.WebPage,
        Title = "Thrift Market",
        BodyText = "Sat 14 Dec, 10am"
    };

    private static readonly RetrievedContent Screenshot = new()
    {
        SourceUrl = null,
        SourceType = SourceType.Image,
        ImageBytes = [1, 2, 3],
        ImageMediaType = "image/png"
    };

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 17, 0, 0, TimeSpan.Zero));
    private readonly FakeLogger<GroqLlmExtractor> _logger = new();

    /// <summary>Answers each call with the next step: a response, or an exception to throw.</summary>
    private sealed class FakeGroqApi(params Func<GroqChatRequest, Task<GroqChatResponse>>[] steps) : IGroqApi
    {
        public List<GroqChatRequest> Requests { get; } = [];

        public Task<GroqChatResponse> CreateChatCompletionAsync(GroqChatRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return steps[Math.Min(Requests.Count, steps.Length) - 1](request);
        }

        public Task<GroqModelList> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new GroqModelList());
    }

    private static Func<GroqChatRequest, Task<GroqChatResponse>> Answer(string content, string finish = "stop") =>
        _ => Task.FromResult(new GroqChatResponse
        {
            Choices = [new GroqChoice { Message = new GroqResponseMessage { Content = content }, FinishReason = finish }],
            Usage = new GroqUsage { PromptTokens = 900, CompletionTokens = 60 }
        });

    private static Func<GroqChatRequest, Task<GroqChatResponse>> Fail(HttpStatusCode status, string body = "", TimeSpan? retryAfter = null) =>
        async _ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (retryAfter is { } wait)
            {
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(wait);
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            throw await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
        };

    private GroqLlmExtractor Extractor(FakeGroqApi api, GroqOptions? options = null) =>
        new(api, options ?? new GroqOptions { ApiKey = "fake-secret-value-for-tests", VisionModel = "vision-model" }, _time, _logger);

    [Fact]
    public async Task AGoodAnswer_IsParsedAndLoggedOnce()
    {
        var api = new FakeGroqApi(Answer(GoodAnswer));

        var fields = await Extractor(api).ExtractAsync(WebPage);

        Assert.Equal("Thrift Market", fields.Title!.Value);
        var request = Assert.Single(api.Requests);
        Assert.Equal("qwen/qwen3.8-27b", request.Model);
        Assert.Equal("none", request.ReasoningEffort);
        Assert.Equal(0, request.Temperature);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Message.Contains("900 prompt + 60 completion tokens"));
    }

    [Fact]
    public async Task TheSystemMessage_CarriesTodayInUkTime()
    {
        var api = new FakeGroqApi(Answer(GoodAnswer));

        await Extractor(api).ExtractAsync(WebPage);

        Assert.Contains("Wednesday 21 October 2026", (string)api.Requests[0].Messages[0].Content);
    }

    [Fact]
    public async Task AScreenshot_GoesToTheVisionModelAsAnImagePart()
    {
        var api = new FakeGroqApi(Answer(GoodAnswer));

        await Extractor(api).ExtractAsync(Screenshot);

        var request = Assert.Single(api.Requests);
        Assert.Equal("vision-model", request.Model);
        var parts = Assert.IsType<List<GroqContentPart>>(request.Messages[1].Content);
        Assert.Contains(parts, p => p.ImageUrl?.Url == "data:image/png;base64,AQID");
    }

    [Fact]
    public async Task AScreenshotWithNoVisionModelConfigured_IsASetupError()
    {
        var api = new FakeGroqApi(Answer(GoodAnswer));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Extractor(api, new GroqOptions { ApiKey = "k" }).ExtractAsync(Screenshot));

        Assert.Contains("Groq:VisionModel", exception.Message);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public async Task ARateLimit_IsWaitedOutThenRetried()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(7)), Answer(GoodAnswer));

        var extraction = Extractor(api).ExtractAsync(WebPage);

        // The extractor is now waiting on the fake clock; move it past Retry-After.
        while (!extraction.IsCompleted)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5);
        }

        var fields = await extraction;
        Assert.Equal("Thrift Market", fields.Title!.Value);
        Assert.Equal(2, api.Requests.Count);
        Assert.Contains(_logger.Collector.GetSnapshot(), r => r.Message.Contains("waiting 7s"));
    }

    [Fact]
    public async Task RateLimitedTooOften_FailsTheExtraction()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(1)));

        var extraction = Extractor(api, new GroqOptions { ApiKey = "k", RateLimitRetries = 1 }).ExtractAsync(WebPage);
        while (!extraction.IsCompleted)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5);
        }

        var exception = await Assert.ThrowsAsync<ExtractionFailedException>(() => extraction);
        Assert.Contains("429", exception.Message);
        Assert.Equal(2, api.Requests.Count);
    }

    [Fact]
    public async Task AnAnswerThatFailedJsonValidation_IsRetriedOnceWithMoreRoom()
    {
        var api = new FakeGroqApi(
            Fail(HttpStatusCode.BadRequest, """{"error":{"code":"json_validate_failed"}}"""),
            Answer(GoodAnswer));

        await Extractor(api).ExtractAsync(WebPage);

        Assert.Equal(2, api.Requests.Count);
        Assert.Equal(2048, api.Requests[1].MaxCompletionTokens);
    }

    [Fact]
    public async Task AServerError_IsExtractionFailedWithGroqsExplanation()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.InternalServerError, """{"error":"upstream overloaded"}"""));

        var exception = await Assert.ThrowsAsync<ExtractionFailedException>(() => Extractor(api).ExtractAsync(WebPage));

        Assert.Contains("500", exception.Message);
        Assert.Contains("upstream overloaded", exception.Message);
    }

    [Fact]
    public async Task ARejectedKey_IsASetupError()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.Unauthorized));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Extractor(api).ExtractAsync(WebPage));

        Assert.Contains("Groq:ApiKey", exception.Message);
    }

    [Theory]
    [InlineData("I'm sorry, I can't read that page.")]
    [InlineData("""{"title": {"value": "Thrift""")]
    [InlineData("")]
    public async Task AnUnusableAnswer_IsExtractionFailed(string answer)
    {
        var api = new FakeGroqApi(Answer(answer, finish: "length"));

        var exception = await Assert.ThrowsAsync<ExtractionFailedException>(() => Extractor(api).ExtractAsync(WebPage));

        Assert.Contains("length", exception.Message);
    }

    [Fact]
    public async Task OurOwnTimeout_IsExtractionFailed()
    {
        var api = new FakeGroqApi(_ => throw new TaskCanceledException("HttpClient.Timeout"));

        await Assert.ThrowsAsync<ExtractionFailedException>(() => Extractor(api).ExtractAsync(WebPage));
    }

    [Fact]
    public async Task CallerCancelling_IsPassedThrough()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var api = new FakeGroqApi(_ => throw new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Extractor(api).ExtractAsync(WebPage, cancel.Token));
    }

    [Fact]
    public async Task TheApiKey_NeverAppearsInTheLog()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(1)), Answer(GoodAnswer));

        var extraction = Extractor(api).ExtractAsync(WebPage);
        while (!extraction.IsCompleted)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5);
        }

        await extraction;
        Assert.DoesNotContain(_logger.Collector.GetSnapshot(), r => r.Message.Contains("fake-secret-value-for-tests"));
    }
}
