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

    /// <summary>Groq's real 429 wording, with the limit and wait it names.</summary>
    private static string RateLimitBody(string limit, string wait) =>
        $$$"""{"error":{"message":"Rate limit reached for model `qwen/qwen3.8-27b` in organization `org_test` service tier `on_demand` on {{{limit}}}: Limit 6000, Used 5900, Requested 900. Please try again in {{{wait}}}. Need more tokens? Upgrade to Dev Tier.","type":"tokens","code":"rate_limit_exceeded"}}""";

    private GroqLlmExtractor Extractor(FakeGroqApi api, GroqOptions? options = null) =>
        new(api, options ?? new GroqOptions { ApiKey = "fake-secret-value-for-tests", VisionModel = "vision-model" }, _time, _logger);

    /// <summary>
    /// Runs an extraction that waits on the fake clock, moving the clock on
    /// until it finishes. Capped, so a retry bug fails the test instead of
    /// hanging the run forever (which in CI means until GitHub's 6-hour limit).
    /// </summary>
    private async Task<T> OnFakeClock<T>(Task<T> extraction, int stepSeconds = 1, int maxSteps = 2000)
    {
        for (var step = 0; !extraction.IsCompleted; step++)
        {
            if (step == maxSteps)
            {
                Assert.Fail($"Still running after {maxSteps * stepSeconds} simulated seconds: a wait that never ends?");
            }

            _time.Advance(TimeSpan.FromSeconds(stepSeconds));
            await Task.Delay(2);
        }

        return await extraction;
    }

    private Task<LlmCardFields> ExtractOnFakeClock(FakeGroqApi api, GroqOptions? options = null, int stepSeconds = 1) =>
        OnFakeClock(Extractor(api, options).ExtractAsync(WebPage), stepSeconds);

    private bool Logged(string text) => _logger.Collector.GetSnapshot().Any(r => r.Message.Contains(text));

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
        Assert.True(Logged("900 prompt + 60 completion tokens"));
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

    // --- Rate limits (429) --------------------------------------------------

    [Fact]
    public async Task ARateLimit_IsWaitedOutUsingRetryAfter()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(7)), Answer(GoodAnswer));

        var fields = await ExtractOnFakeClock(api);

        Assert.Equal("Thrift Market", fields.Title!.Value);
        Assert.Equal(2, api.Requests.Count);
        Assert.True(Logged("per-minute limit reached"));
        Assert.True(Logged("waiting 7s"));
    }

    [Fact]
    public async Task WithoutRetryAfter_TheWaitInGroqsMessageIsUsed()
    {
        var api = new FakeGroqApi(
            Fail(HttpStatusCode.TooManyRequests, RateLimitBody("tokens per minute (TPM)", "7.5s")),
            Answer(GoodAnswer));

        await ExtractOnFakeClock(api);

        Assert.True(Logged("waiting 7.5s"));
    }

    [Fact]
    public async Task WithNoWaitGivenAtAll_AMinuteIsAssumedWithinTheCeiling()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.TooManyRequests, """{"error":"slow down"}"""), Answer(GoodAnswer));

        await ExtractOnFakeClock(api);

        // A minute, capped by the default 30 s ceiling.
        Assert.True(Logged("waiting 30s"));
    }

    [Fact]
    public async Task ADailyLimit_FailsAtOnceSayingWhenItResets()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.TooManyRequests, RateLimitBody("tokens per day (TPD)", "3h12m5.2s")));

        var exception = await Assert.ThrowsAsync<ExtractionFailedException>(() => Extractor(api).ExtractAsync(WebPage));

        Assert.Single(api.Requests);
        Assert.Contains("daily limit", exception.Message);
        Assert.Contains("resets in 3h 12m", exception.Message);
        Assert.Contains("Groq:MaxRateLimitWaitSeconds", exception.Message);
    }

    [Fact]
    public async Task APerMinuteWaitOverTheCeiling_FailsAtOnce()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.TooManyRequests, RateLimitBody("tokens per minute (TPM)", "1m30s")));

        var exception = await Assert.ThrowsAsync<ExtractionFailedException>(() => Extractor(api).ExtractAsync(WebPage));

        Assert.Single(api.Requests);
        Assert.Contains("per-minute limit", exception.Message);
        Assert.Contains("1m 30s", exception.Message);
    }

    [Fact]
    public async Task WithAHigherCeiling_LikeEval_ALongWaitIsSatThrough()
    {
        var api = new FakeGroqApi(
            Fail(HttpStatusCode.TooManyRequests, RateLimitBody("tokens per minute (TPM)", "1m30s")),
            Answer(GoodAnswer));

        var fields = await ExtractOnFakeClock(
            api, new GroqOptions { ApiKey = "k", MaxRateLimitWaitSeconds = 900 }, stepSeconds: 10);

        Assert.Equal("Thrift Market", fields.Title!.Value);
        Assert.True(Logged("waiting 1m 30s"));
    }

    [Fact]
    public async Task RateLimitedTooOften_FailsTheExtraction()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(1)));

        var exception = await Assert.ThrowsAsync<ExtractionFailedException>(() =>
            ExtractOnFakeClock(api, new GroqOptions { ApiKey = "k", RateLimitRetries = 1 }));

        Assert.Contains("still rate limited", exception.Message);
        Assert.Equal(2, api.Requests.Count);
    }

    // --- Momentary failures: 5xx, network, our timeout -----------------------

    [Fact]
    public async Task AServerError_IsRetriedWithGrowingWaits()
    {
        var api = new FakeGroqApi(
            Fail(HttpStatusCode.ServiceUnavailable, """{"error":"overloaded"}"""),
            Fail(HttpStatusCode.BadGateway),
            Answer(GoodAnswer));

        var fields = await ExtractOnFakeClock(api);

        Assert.Equal("Thrift Market", fields.Title!.Value);
        Assert.Equal(3, api.Requests.Count);
        Assert.True(Logged("retrying in 5s (retry 1 of 3)"));
        Assert.True(Logged("retrying in 10s (retry 2 of 3)"));
    }

    [Fact]
    public async Task AServerErrorThatPersists_IsExtractionFailedWithGroqsExplanation()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.InternalServerError, """{"error":"upstream overloaded"}"""));

        var exception = await Assert.ThrowsAsync<ExtractionFailedException>(() => ExtractOnFakeClock(api));

        Assert.Equal(4, api.Requests.Count);
        Assert.Contains("500", exception.Message);
        Assert.Contains("upstream overloaded", exception.Message);
        Assert.Contains("tried 4 times", exception.Message);
    }

    [Fact]
    public async Task ANetworkFailure_IsRetried()
    {
        var api = new FakeGroqApi(_ => throw new HttpRequestException("Connection reset"), Answer(GoodAnswer));

        await ExtractOnFakeClock(api);

        Assert.Equal(2, api.Requests.Count);
        Assert.True(Logged("Groq couldn't be reached: Connection reset; retrying in 5s"));
    }

    [Fact]
    public async Task OurOwnTimeout_IsRetriedThenFails()
    {
        var api = new FakeGroqApi(_ => throw new TaskCanceledException("HttpClient.Timeout"));

        var exception = await Assert.ThrowsAsync<ExtractionFailedException>(() => ExtractOnFakeClock(api));

        Assert.Contains("didn't answer within 60s", exception.Message);
        Assert.Equal(4, api.Requests.Count);
    }

    [Fact]
    public async Task WithNoTransientRetries_AServerErrorFailsAtOnce()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<ExtractionFailedException>(() =>
            Extractor(api, new GroqOptions { ApiKey = "k", TransientRetries = 0 }).ExtractAsync(WebPage));

        Assert.Single(api.Requests);
    }

    // --- Everything else ------------------------------------------------------

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
    public async Task ARejectedKey_IsASetupError()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.Unauthorized));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Extractor(api).ExtractAsync(WebPage));

        Assert.Contains("Groq:ApiKey", exception.Message);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task ARetiredModel_IsASetupErrorNamingTheSetting()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.NotFound, """{"error":{"message":"The model does not exist"}}"""));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Extractor(api).ExtractAsync(WebPage));

        Assert.Contains("'qwen/qwen3.8-27b'", exception.Message);
        Assert.Contains("Groq:TextModel", exception.Message);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task ARetiredVisionModel_NamesTheVisionSetting()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.NotFound));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Extractor(api).ExtractAsync(Screenshot));

        Assert.Contains("Groq:VisionModel", exception.Message);
    }

    [Fact]
    public async Task AnyOther4xx_IsExtractionFailedWithoutRetrying()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.RequestEntityTooLarge, """{"error":"request too large"}"""));

        var exception = await Assert.ThrowsAsync<ExtractionFailedException>(() => Extractor(api).ExtractAsync(WebPage));

        Assert.Contains("413", exception.Message);
        Assert.Single(api.Requests);
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
    public async Task CallerCancelling_IsPassedThrough()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var api = new FakeGroqApi(_ => throw new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Extractor(api).ExtractAsync(WebPage, cancel.Token));
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task CallerCancellingDuringAWait_StopsTheWait()
    {
        using var cancel = new CancellationTokenSource();
        var api = new FakeGroqApi(Fail(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(20)));

        var extraction = Extractor(api).ExtractAsync(WebPage, cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extraction);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task TheApiKey_NeverAppearsInTheLog()
    {
        var api = new FakeGroqApi(Fail(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(1)), Answer(GoodAnswer));

        await ExtractOnFakeClock(api);

        Assert.False(Logged("fake-secret-value-for-tests"));
    }
}
