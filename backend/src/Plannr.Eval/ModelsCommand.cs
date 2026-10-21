using Plannr.Infrastructure.Llm;
using Plannr.Infrastructure.Options;

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
        var models = await groq.ListModelsAsync(cancellationToken);

        foreach (var model in models.Data.OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            var role = model.Id == options.TextModel ? "  <- Groq:TextModel"
                : model.Id == options.VisionModel ? "  <- Groq:VisionModel"
                : string.Empty;
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
