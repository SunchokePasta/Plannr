using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Plannr.Api.Middleware;
using Plannr.Api.Models;
using Plannr.Core.Diagnostics;
using Plannr.Core.Exceptions;
using Plannr.Tests.Diagnostics;

namespace Plannr.Tests.Api;

/// <summary>
/// The middleware called directly with a fake HttpContext: no server, no
/// network. In the ErrorReference collection because it reads the static
/// program code (and asserts on it).
/// </summary>
[Collection(ErrorReferenceCollection.Name)]
public sealed class ExceptionHandlingMiddlewareTests : IDisposable
{
    private readonly FakeLogger<ExceptionHandlingMiddleware> _logger = new();

    public ExceptionHandlingMiddlewareTests() =>
        ErrorReference.Configure(ErrorReference.ApiProgram, ErrorReference.MainLog);

    public void Dispose() => ErrorReference.Reset();

    [Fact]
    public async Task NoException_PassesThroughAndLogsNothing()
    {
        var context = NewContext();

        await Run(context, ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal(0, _logger.Collector.Count);
    }

    [Fact]
    public async Task DbException_Gives503AndTheSameReferenceInBodyAndLog()
    {
        var context = NewContext();

        await Run(context, _ => throw new FakeDbException());

        var body = ReadBody(context);
        var log = _logger.Collector.LatestRecord;

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        Assert.Equal(503, body.Status);
        Assert.Matches("^[0-9a-f]{8}:01:01$", body.Reference);
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.IsType<FakeDbException>(log.Exception);
        Assert.Equal(body.Reference, StateValue(log, "Reference"));
    }

    [Fact]
    public async Task ArgumentException_Gives400LoggedAsWarning()
    {
        var context = NewContext();

        await Run(context, _ => throw new ArgumentException("bad id"));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(LogLevel.Warning, _logger.Collector.LatestRecord.Level);
    }

    [Fact]
    public async Task ExceptionMessage_NeverReachesTheResponseBody()
    {
        var context = NewContext();

        await Run(context, _ => throw new InvalidOperationException("Server=db;Password=hunter2"));

        var raw = ReadRawBody(context);

        Assert.DoesNotContain("hunter2", raw);
        Assert.Equal("The API is misconfigured.", ReadBody(context).Message);
    }

    [Fact]
    public async Task CallerHangingUp_Gives499AtInformationWithNoBody()
    {
        using var hangUp = new CancellationTokenSource();
        var context = NewContext();
        context.RequestAborted = hangUp.Token;

        await Run(context, _ =>
        {
            hangUp.Cancel();
            throw new OperationCanceledException(hangUp.Token);
        });

        Assert.Equal(ExceptionHandlingMiddleware.ClientClosedRequest, context.Response.StatusCode);
        Assert.Equal(LogLevel.Information, _logger.Collector.LatestRecord.Level);
        Assert.Empty(ReadRawBody(context));
    }

    [Fact]
    public async Task OurOwnTimeout_Gives504()
    {
        var context = NewContext();

        await Run(context, _ => throw new TaskCanceledException());

        Assert.Equal(StatusCodes.Status504GatewayTimeout, context.Response.StatusCode);
        Assert.Equal(LogLevel.Error, _logger.Collector.LatestRecord.Level);
    }

    [Fact]
    public async Task UnsupportedLink_Gives400WithTheUserMessageOnly()
    {
        var context = NewContext();
        var exception = new UnsupportedLinkException("ftp://files.example.com/flyer.pdf is not http(s)");

        await Run(context, _ => throw exception);

        var raw = ReadRawBody(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(exception.UserMessage, ReadBody(context).Message);
        Assert.DoesNotContain("ftp://", raw);
        Assert.Equal(LogLevel.Warning, _logger.Collector.LatestRecord.Level);
    }

    [Fact]
    public async Task UploadTooLarge_Gives413()
    {
        var context = NewContext();

        await Run(context, _ => throw new BadHttpRequestException("too big", StatusCodes.Status413PayloadTooLarge));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.Equal("That upload is too large.", ReadBody(context).Message);
    }

    [Fact]
    public async Task ResponseAlreadyStarted_WritesNoBodyAndWarns()
    {
        var context = NewContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        await Run(context, _ => throw new FakeDbException());

        var records = _logger.Collector.GetSnapshot();

        Assert.Empty(ReadRawBody(context));
        Assert.Contains(records, r => r.Level == LogLevel.Error);
        Assert.Contains(records, r => r.Level == LogLevel.Warning && r.Message.Contains("already started"));
    }

    private Task Run(HttpContext context, RequestDelegate next) =>
        new ExceptionHandlingMiddleware(next, _logger).InvokeAsync(context);

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/v1/cards/1";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadRawBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }

    private static ApiErrorResponse ReadBody(HttpContext context) =>
        JsonSerializer.Deserialize<ApiErrorResponse>(ReadRawBody(context), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException("The response body was empty.");

    private static string? StateValue(FakeLogRecord record, string key) =>
        record.StructuredState?.FirstOrDefault(pair => pair.Key == key).Value;

    /// <summary>DbException is abstract; MySqlException is the real one.</summary>
    private sealed class FakeDbException : DbException;

    /// <summary>A response whose headers are already on the wire.</summary>
    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
