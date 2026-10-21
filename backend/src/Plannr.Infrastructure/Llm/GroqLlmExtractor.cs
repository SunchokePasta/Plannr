using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plannr.Core.Capture;
using Plannr.Core.Exceptions;
using Plannr.Core.Interfaces;
using Plannr.Infrastructure.Options;
using Refit;

namespace Plannr.Infrastructure.Llm;

/// <summary>
/// Retrieved content -> one Groq JSON-mode call -> <see cref="LlmCardFields"/>.
/// Text goes to the text model; a screenshot to the vision model.
///
/// What it does about Groq going wrong (Applr's CvBulletWriter and
/// JobAnalyser rules):
///  - 429 rate limited: waits as long as Groq asks (Retry-After, else the
///    time in its message), up to RateLimitRetries times. A wait longer than
///    MaxRateLimitWaitSeconds -- typically the daily limit -- fails at once,
///    saying when the limit resets, instead of retrying into the same wall;
///  - network failures, our own timeout and 5xx: retried TransientRetries
///    times with growing waits, since they are usually momentary;
///  - 400 json_validate_failed (usually an empty answer): retried once at
///    temperature 0 with more output allowance;
///  - 401/403 (bad key) and 404 (retired model): setup problems, thrown as
///    InvalidOperationException naming the setting to fix;
///  - anything else, and unreadable answers: ExtractionFailedException, with
///    Groq's own explanation in Message for the log.
/// The full prompt contains page text, so it is logged at Debug only. The
/// key is never logged: it isn't even visible here, only in the HttpClient.
/// </summary>
public sealed class GroqLlmExtractor(
    IGroqApi groq,
    GroqOptions options,
    TimeProvider time,
    ILogger<GroqLlmExtractor> logger) : ILlmExtractor
{
    /// <summary>
    /// When a 429 doesn't say how long to wait. A minute covers any
    /// per-minute limit (JobAnalyser's choice), capped by the program's ceiling.
    /// </summary>
    private static readonly TimeSpan UnknownRateLimitWait = TimeSpan.FromMinutes(1);

    private const int PreviewChars = 300;

    public async Task<LlmCardFields> ExtractAsync(RetrievedContent content, CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(content, out var modelSetting);
        var stopwatch = Stopwatch.StartNew();

        var response = await SendAsync(request, modelSetting, cancellationToken);

        var choice = response.Choices.FirstOrDefault();
        var answer = choice?.Message.Content;

        logger.LogInformation(
            "Groq {Model} answered in {ElapsedMs} ms: {PromptTokens} prompt + {CompletionTokens} completion tokens, finish {FinishReason}.",
            request.Model,
            stopwatch.ElapsedMilliseconds,
            response.Usage?.PromptTokens,
            response.Usage?.CompletionTokens,
            choice?.FinishReason);

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new ExtractionFailedException(
                $"Groq {request.Model} returned an empty answer (finish {choice?.FinishReason ?? "none"}).");
        }

        try
        {
            return LlmAnswerParser.Parse(answer);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            // "length" here means the answer was cut off mid-JSON.
            throw new ExtractionFailedException(
                $"Groq {request.Model}'s answer wasn't the expected JSON (finish {choice?.FinishReason}): {Preview(answer)}",
                exception);
        }
    }

    private GroqChatRequest BuildRequest(RetrievedContent content, out string modelSetting)
    {
        var isImage = content.ImageBytes is not null;

        if (isImage && string.IsNullOrWhiteSpace(options.VisionModel))
        {
            throw new InvalidOperationException(
                $"{GroqOptions.SectionName}:{nameof(GroqOptions.VisionModel)} is not set, so screenshots can't be read. " +
                "List the models your key can use with 'Plannr.Eval models' and set one that accepts images.");
        }

        modelSetting = $"{GroqOptions.SectionName}:{(isImage ? nameof(GroqOptions.VisionModel) : nameof(GroqOptions.TextModel))}";
        var model = isImage ? options.VisionModel : options.TextModel;
        var system = ExtractionPrompt.SystemMessage(UkTime.Now(time));
        var user = ExtractionPrompt.UserMessage(content);

        logger.LogDebug(
            "Groq prompt for {Source} ({Chars} characters):\n{System}\n---\n{User}",
            content.SourceUrl?.ToString() ?? "a screenshot", system.Length + user.Length, system, user);

        object userContent = isImage
            ? new List<GroqContentPart>
            {
                GroqContentPart.ForText(user),
                GroqContentPart.ForImage(content.ImageBytes!, content.ImageMediaType ?? "image/png")
            }
            : user;

        return new GroqChatRequest
        {
            Model = model,
            Messages =
            [
                new GroqChatMessage { Role = "system", Content = system },
                new GroqChatMessage { Role = "user", Content = userContent }
            ],
            Temperature = options.Temperature,
            MaxCompletionTokens = options.MaxOutputTokens,
            ReasoningEffort = options.ReasoningEffortFor(model)
        };
    }

    private async Task<GroqChatResponse> SendAsync(GroqChatRequest request, string modelSetting, CancellationToken cancellationToken)
    {
        var rateLimitWaits = 0;
        var transientFailures = 0;
        var retriedEmptyAnswer = false;

        while (true)
        {
            try
            {
                return await groq.CreateChatCompletionAsync(request, cancellationToken);
            }
            catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitWaits++;
                await WaitOutRateLimitAsync(exception, request.Model, rateLimitWaits, cancellationToken);
            }
            catch (ApiException exception)
                when (exception.StatusCode == HttpStatusCode.BadRequest
                    && exception.Content?.Contains("json_validate_failed", StringComparison.Ordinal) == true
                    && !retriedEmptyAnswer)
            {
                retriedEmptyAnswer = true;
                request.Temperature = 0;
                request.MaxCompletionTokens = Math.Min(options.MaxOutputTokens * 2, 8000);

                logger.LogInformation(
                    "Groq {Model} gave an answer that wasn't JSON; retrying once with {Tokens} tokens at temperature 0.",
                    request.Model, request.MaxCompletionTokens);
            }
            catch (ApiException exception)
                when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException(
                    $"Groq refused the API key ({(int)exception.StatusCode}). Check {GroqOptions.SectionName}:{nameof(GroqOptions.ApiKey)}.",
                    exception);
            }
            catch (ApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                // Groq retires models (llama-3.3-70b went in August 2026).
                throw new InvalidOperationException(
                    $"Groq doesn't recognise the model '{request.Model}' (404); it may have been retired. " +
                    $"Change {modelSetting} -- 'Plannr.Eval models' lists what your key can use. Groq said: {Preview(exception.Content)}",
                    exception);
            }
            catch (ApiException exception) when ((int)exception.StatusCode >= 500)
            {
                transientFailures++;
                await RetryTransientAsync(
                    exception, $"Groq {request.Model} returned {(int)exception.StatusCode}: {Preview(exception.Content)}",
                    transientFailures, cancellationToken);
            }
            catch (ApiException exception)
            {
                throw new ExtractionFailedException(
                    $"Groq {request.Model} returned {(int)exception.StatusCode}: {Preview(exception.Content)}", exception);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                transientFailures++;
                await RetryTransientAsync(
                    exception, $"Groq {request.Model} didn't answer within {options.TimeoutSeconds}s",
                    transientFailures, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                transientFailures++;
                await RetryTransientAsync(
                    exception, $"Groq couldn't be reached: {exception.Message}",
                    transientFailures, cancellationToken);
            }
        }
    }

    /// <summary>Waits as long as Groq asked, or throws when that's too long or this has happened too often.</summary>
    private async Task WaitOutRateLimitAsync(ApiException exception, string model, int waits, CancellationToken cancellationToken)
    {
        var limit = GroqRateLimit.Read(exception.Headers?.RetryAfter, exception.Content, time.GetUtcNow());
        var scope = limit.IsDaily ? "daily" : "per-minute";
        var ceiling = TimeSpan.FromSeconds(options.MaxRateLimitWaitSeconds);

        if (waits > options.RateLimitRetries)
        {
            throw new ExtractionFailedException(
                $"Groq {model} was still rate limited ({scope}) after {options.RateLimitRetries} wait(s).", exception);
        }

        if (limit.Wait > ceiling)
        {
            // A known wait we won't sit through: say when it resets, so the
            // log (and Eval's output) shows "come back later", not "broken".
            throw new ExtractionFailedException(
                $"Groq's {scope} limit for {model} resets in {GroqRateLimit.Describe(limit.Wait.Value)}, longer than " +
                $"{GroqOptions.SectionName}:{nameof(GroqOptions.MaxRateLimitWaitSeconds)} ({options.MaxRateLimitWaitSeconds}s).",
                exception);
        }

        // An unknown wait isn't a reason to give up: assume a per-minute
        // limit and wait a minute, or as long as this program allows.
        var wait = limit.Wait ?? (UnknownRateLimitWait < ceiling ? UnknownRateLimitWait : ceiling);

        logger.LogInformation(
            "Groq {Scope} limit reached for {Model}; waiting {Wait} (retry {Attempt} of {Retries}).",
            scope, model, GroqRateLimit.Describe(wait), waits, options.RateLimitRetries);

        await Task.Delay(wait, time, cancellationToken);
    }

    /// <summary>Waits a little longer each time, or throws once the retries are used up.</summary>
    private async Task RetryTransientAsync(Exception exception, string what, int failures, CancellationToken cancellationToken)
    {
        if (failures > options.TransientRetries)
        {
            throw new ExtractionFailedException($"{what} (tried {failures} times).", exception);
        }

        var wait = TimeSpan.FromSeconds(options.TransientRetryBaseSeconds * failures);

        logger.LogInformation(
            "{What}; retrying in {Wait} (retry {Attempt} of {Retries}).",
            what, GroqRateLimit.Describe(wait), failures, options.TransientRetries);

        await Task.Delay(wait, time, cancellationToken);
    }

    private static string Preview(string? text) =>
        string.IsNullOrEmpty(text) ? "(empty)"
        : text.Length <= PreviewChars ? text
        : text[..PreviewChars] + "...";
}
