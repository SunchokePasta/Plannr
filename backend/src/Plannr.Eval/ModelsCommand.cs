using System.Net;
using Plannr.Infrastructure.Llm;
using Plannr.Infrastructure.Options;
using Refit;

namespace Plannr.Eval;

/// <summary>
/// <c>models</c>: what this Groq key can use, and which configured model
/// is which. Groq's list doesn't say which models accept images, so the
/// VisionModel choice still needs Groq's docs -- this just shows what exists.
/// </summary>
public static class ModelsCommand
{
    public static async Task<int> RunAsync(IGroqApi groq, GroqOptions options, TextWriter output, CancellationToken cancellationToken)
    {
        GroqModelList models;
        try
        {
            models = await groq.ListModelsAsync(cancellationToken);
        }
        catch (ApiException exception)
            when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // The same wording the extractor uses, instead of Refit's raw
            // "Response status code does not indicate success: 401".
            throw new InvalidOperationException(
                $"Groq refused the API key ({(int)exception.StatusCode}). Check {GroqOptions.SectionName}:{nameof(GroqOptions.ApiKey)}.",
                exception);
        }

        foreach (var model in models.Data.OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            // One model can be both (qwen3.8 reads text and images).
            var roles = new[] { (options.TextModel, "Groq:TextModel"), (options.VisionModel, "Groq:VisionModel") }
                .Where(r => r.Item1 == model.Id)
                .Select(r => r.Item2)
                .ToList();
            var role = roles.Count == 0 ? string.Empty : "  <- " + string.Join(", ", roles);
            var inactive = model.Active == false ? " (inactive)" : string.Empty;

            await output.WriteLineAsync($"{model.Id,-50} {model.OwnedBy,-14} {model.ContextWindow,8}{inactive}{role}");
        }

        await WarnIfMissingAsync(output, models, options.TextModel, nameof(GroqOptions.TextModel));
        await WarnIfMissingAsync(output, models, options.VisionModel, nameof(GroqOptions.VisionModel));

        return 0;
    }

    private static async Task WarnIfMissingAsync(TextWriter output, GroqModelList models, string configured, string setting)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            await output.WriteLineAsync($"\nGroq:{setting} is not set.");
        }
        else if (models.Data.All(m => m.Id != configured))
        {
            await output.WriteLineAsync($"\nGroq:{setting} is '{configured}', which this key can't use. Pick one from the list above.");
        }
    }
}
