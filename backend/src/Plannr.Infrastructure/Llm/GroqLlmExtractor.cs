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
/// What it does about Groq going wrong (Applr's CvBulletWriter rules):
///  - 429 rate limited: waits Groq's Retry-After, up to RateLimitRetries times;
///  - 400 json_validate_failed (usually an empty answer): retried once at
///    temperature 0 with more output allowance;
///  - 401/403: the key is wrong -- a setup problem, thrown as
///    InvalidOperationException so it reads as one;
///  - anything else, timeouts and unreadable answers: ExtractionFailedException,
///    with Groq's own explanation in Message for the log.
/// The full prompt contains page text, so it is logged at Debug only. The
/// key is never logged: it isn't even visible here, only in the HttpClient.
/// </summary>
public sealed class GroqLlmExtractor(
    IGroqApi groq,
    GroqOptions options,
    TimeProvider time,
    ILogger<GroqLlmExtractor> logger) : ILlmExtractor
{
    private static readonly TimeSpan DefaultRateLimitWait = TimeSpan.FromSeconds(5);
    private const int PreviewChars = 300;

    public async Task<LlmCardFields> ExtractAsync(RetrievedContent content, CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(content);
        var stopwatch = Stopwatch.StartNew();

        var response = await SendAsync(request, cancellationToken);

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

    private GroqChatRequest BuildRequest(RetrievedContent content)
    {
        var isImage = content.ImageBytes is not null;

        if (isImage && string.IsNullOrWhiteSpace(options.VisionModel))
        {
            throw new InvalidOperationException(
                $"{GroqOptions.SectionName}:{nameof(GroqOptions.VisionModel)} is not set, so screenshots can't be read. " +
                "List the models your key can use with 'Plannr.Eval models' and set one that accepts images.");
        }

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

    private async Task<GroqChatResponse> SendAsync(GroqChatRequest request, CancellationToken cancellationToken)
    {
        var retriedEmptyAnswer = false;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await groq.CreateChatCompletionAsync(request, cancellationToken);
            }
            catch (ApiException exception)
                when (exception.StatusCode == HttpStatusCode.TooManyRequests && attempt < options.RateLimitRetries)
            {
                var wait = RetryAfter(exception);
                logger.LogInformation(
                    "Groq rate limit reached; waiting {Seconds}s (retry {Attempt} of {Retries}).",
                    Math.Round(wait.TotalSeconds, 1), attempt + 1, options.RateLimitRetries);

                await Task.Delay(wait, time, cancellationToken);
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
                throw new ExtractionFailedException(
                    $"Groq {request.Model} didn't answer within {options.TimeoutSeconds}s.", exception);
            }
            catch (HttpRequestException exception)
            {
                throw new ExtractionFailedException($"Groq couldn't be reached: {exception.Message}", exception);
            }
        }
    }

    /// <summary>How long Groq asked us to wait (Retry-After), within sensible bounds.</summary>
    private TimeSpan RetryAfter(ApiException exception)
    {
        var header = exception.Headers?.RetryAfter;
        var wait = header?.Delta
            ?? (header?.Date is { } date ? date - time.GetUtcNow() : (TimeSpan?)null)
            ?? DefaultRateLimitWait;

        return TimeSpan.FromSeconds(Math.Clamp(wait.TotalSeconds, 1, options.MaxRateLimitWaitSeconds));
    }

    private static string Preview(string? text) =>
        string.IsNullOrEmpty(text) ? "(empty)"
        : text.Length <= PreviewChars ? text
        : text[..PreviewChars] + "...";
}
