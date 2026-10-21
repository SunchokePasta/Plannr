using System.Data.Common;
using System.Text.Json;
using Plannr.Api.Models;
using Plannr.Core.Diagnostics;

namespace Plannr.Api.Middleware;

/// <summary>
/// One place where every unhandled exception in this API is caught,
/// logged with a reference id, and turned into an
/// <see cref="ApiErrorResponse"/>. Registered first in the pipeline (see
/// Program.cs) so it wraps model binding, routing, controllers and EF alike.
///
/// The point is that controllers and services don't need their own
/// try/catch. Let the exception escape and it lands here with its stack
/// trace in the log. Only catch something lower down when the catch
/// changes behaviour -- a duplicate insert that is really a no-op, say --
/// not to log and rethrow.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>nginx's "client closed request". There is no standard code for it.</summary>
    public const int ClientClosedRequest = 499;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            // The phone hung up mid-request (app closed, lost signal, its
            // own timeout). Nothing is broken here, so this is Information,
            // not Error -- otherwise every abandoned request looks like a
            // fault in the log.
            logger.LogInformation(
                "{Method} {Path} was cancelled by the caller.",
                context.Request.Method,
                context.Request.Path);

            if (!context.Response.HasStarted)
            {
                // Nothing is sent back -- there is nobody left to read it.
                context.Response.StatusCode = ClientClosedRequest;
            }
        }
        catch (Exception exception)
        {
            await WriteErrorAsync(context, exception);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var (status, message) = Describe(exception);
        var reference = ErrorReference.New();

        logger.Log(
            status >= StatusCodes.Status500InternalServerError
                ? LogLevel.Error
                : LogLevel.Warning,
            exception,
            "[{Reference}] {Method} {Path} failed with {StatusCode}. TraceId {TraceId}.",
            reference,
            context.Request.Method,
            context.Request.Path,
            status,
            context.TraceIdentifier);

        if (context.Response.HasStarted)
        {
            // Headers are already on the wire (a partially streamed
            // response). We can't rewrite the status, and appending JSON
            // would corrupt the body -- the log line above is all we get.
            logger.LogWarning(
                "[{Reference}] The response had already started; no error body was sent.",
                reference);

            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(
                new ApiErrorResponse
                {
                    Message = message,
                    Reference = reference,
                    Status = status
                },
                JsonOptions),
            context.RequestAborted);
    }

    /// <summary>
    /// Exception type -> (status code, message a human may read). The
    /// messages here are intentionally vague about internals: the
    /// specifics live in the log line the reference id points at.
    /// Order matters -- the first matching arm wins, so specific types go
    /// above the general ones they derive from.
    /// </summary>
    internal static (int Status, string Message) Describe(Exception exception) =>
        exception switch
        {
            // Kestrel's own rejections, e.g. a screenshot upload over the
            // body size limit. They already know the right status code.
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                (StatusCodes.Status413PayloadTooLarge,
                    "That upload is too large."),

            BadHttpRequestException badRequest =>
                (badRequest.StatusCode,
                    "The request could not be understood."),

            // Covers MySqlException: server down, bad credentials,
            // connection pool exhausted, dropped mid-query. (EF's
            // DbUpdateException -> 409 arrives with EF in Milestone 3.)
            DbException =>
                (StatusCodes.Status503ServiceUnavailable,
                    "The database is not reachable right now."),

            // A cancellation that isn't the caller hanging up (handled
            // above) is a timeout on our side.
            OperationCanceledException =>
                (StatusCodes.Status504GatewayTimeout,
                    "The request took too long and was stopped."),

            // Bad route values or a malformed body that got past model
            // binding -- the caller's problem, not ours.
            ArgumentException or FormatException =>
                (StatusCodes.Status400BadRequest,
                    "The request could not be understood."),

            // Thrown by Program.cs and GetValidated for missing
            // configuration, and by anything that finds itself in an
            // impossible state.
            InvalidOperationException =>
                (StatusCodes.Status500InternalServerError,
                    "The API is misconfigured."),

            _ => (StatusCodes.Status500InternalServerError,
                    "Something went wrong.")
        };
}
