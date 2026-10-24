using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Plannr.Services.Diagnostics;
using Plannr.Services.DTOs;
using Plannr.Services.Exceptions;
using Plannr.Tests.Diagnostics;
using Plannr.Worker.HandOffs;
using Plannr.Worker.Options;
using Refit;

namespace Plannr.Tests.HandOffs;

/// <summary>H4.6-H4.7: one job, and the worker's error boundary.</summary>
[Collection(ErrorReferenceCollection.Name)]
public sealed class CaptureJobProcessorTests : IDisposable
{
    private static readonly ClaimedCapture Job = new(42, HandOffTestData.Link, 1);

    private readonly FakePlannrInternalApi _api = new();
    private readonly FakeLogger<CaptureJobProcessor> _logger = new();
    private readonly WorkerOptions _options = new() { Secret = HandOffTestData.Secret, ReportAttempts = 3, ReportRetryBaseSeconds = 0 };

    public CaptureJobProcessorTests() => ErrorReference.Configure(ErrorReference.WorkerProgram, ErrorReference.MainLog);

    public void Dispose() => ErrorReference.Reset();

    private CaptureJobProcessor Processor(Func<Uri, CardDraft> pipeline) =>
        new(new FakePipeline(pipeline), _api, _options, new FakeTimeProvider(), _logger);

    private static async Task<ApiException> Refused(HttpStatusCode status) =>
        await ApiException.Create(
            new HttpRequestMessage(HttpMethod.Post, "http://api.test/internal/captures/42/result"), HttpMethod.Post,
            new HttpResponseMessage(status) { Content = new StringContent("", Encoding.UTF8, "application/json") },
            new RefitSettings());

    [Fact(Skip = Todo.H4_7)]
    public async Task ACard_IsHandedBack()
    {
        await Processor(_ => HandOffTestData.Card()).ProcessAsync(Job, CancellationToken.None);

        var (captureId, result) = Assert.Single(_api.Reported);
        Assert.Equal(42, captureId);
        Assert.Equal("Thrift Market", result.Card!.Title);
        Assert.Null(result.Failure);
    }

    [Fact(Skip = Todo.H4_7)]
    public async Task AnExpectedFailure_IsHandedBackWithItsUserMessageAndAWorkerReference()
    {
        await Processor(_ => throw new ContentUnavailableException("GET https://venue.example.com/... returned 404"))
            .ProcessAsync(Job, CancellationToken.None);

        var failure = Assert.Single(_api.Reported).Result.Failure!;
        Assert.Equal("We couldn't open that link. You can fill the card in yourself.", failure.UserMessage);
        Assert.Matches(@"^[0-9a-f]{8}:02:01$", failure.ErrorReference);

        var log = _logger.Collector.LatestRecord;
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.StartsWith($"[{failure.ErrorReference}]", log.Message);
    }

    [Fact(Skip = Todo.H4_7)]
    public async Task ABug_IsHandedBackAsAPlainMessageAndLoggedAsAnError()
    {
        await Processor(_ => throw new NullReferenceException()).ProcessAsync(Job, CancellationToken.None);

        var failure = Assert.Single(_api.Reported).Result.Failure!;
        Assert.Equal(CaptureJobProcessor.UnexpectedFailureMessage, failure.UserMessage);
        Assert.Equal(LogLevel.Error, _logger.Collector.LatestRecord.Level);
        Assert.IsType<NullReferenceException>(_logger.Collector.LatestRecord.Exception);
    }

    [Fact(Skip = Todo.H4_7)]
    public async Task HandingBack_IsRetriedWhileTheApiIsUnreachable()
    {
        _api.ReportFailures.Enqueue(new HttpRequestException("Connection refused"));
        _api.ReportFailures.Enqueue(await Refused(HttpStatusCode.ServiceUnavailable));

        await Processor(_ => HandOffTestData.Card()).ProcessAsync(Job, CancellationToken.None);

        Assert.Equal(3, _api.Reported.Count);
    }

    [Fact(Skip = Todo.H4_7)]
    public async Task A4xx_IsNotRetried()
    {
        _api.ReportFailures.Enqueue(await Refused(HttpStatusCode.Unauthorized));

        await Processor(_ => HandOffTestData.Card()).ProcessAsync(Job, CancellationToken.None);

        Assert.Single(_api.Reported);
        Assert.Equal(LogLevel.Error, _logger.Collector.LatestRecord.Level);
    }

    [Fact(Skip = Todo.H4_7)]
    public async Task GivingUp_IsLoggedAndDoesntThrow()
    {
        for (var i = 0; i < 5; i++)
        {
            _api.ReportFailures.Enqueue(new HttpRequestException("Connection refused"));
        }

        await Processor(_ => HandOffTestData.Card()).ProcessAsync(Job, CancellationToken.None);

        Assert.Equal(3, _api.Reported.Count);
        Assert.Equal(LogLevel.Error, _logger.Collector.LatestRecord.Level);
    }

    [Fact(Skip = Todo.H4_6)]
    public async Task ShuttingDown_IsNotAFailure()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Processor(_ => throw new OperationCanceledException(cancel.Token)).ProcessAsync(Job, cancel.Token));

        Assert.Empty(_api.Reported);
    }
}
