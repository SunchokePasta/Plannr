using Plannr.Core.Interfaces;

namespace Plannr.Eval;

/// <summary>
/// <c>extract &lt;url&gt;</c> and <c>extract --image &lt;path&gt;</c>: run the
/// pipeline once and print the card. Failures aren't caught here -- they go
/// to Program.cs's boundary, which logs them with a reference.
/// </summary>
public static class ExtractCommands
{
    public static async Task<int> LinkAsync(IExtractionPipeline pipeline, Uri url, TextWriter output, CancellationToken cancellationToken)
    {
        var card = await pipeline.ExtractFromLinkAsync(url, cancellationToken);
        await output.WriteLineAsync(CardJson.Serialize(card));
        return 0;
    }

    public static async Task<int> ImageAsync(IExtractionPipeline pipeline, string path, TextWriter output, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);

        // Only a hint: the pipeline decides the real type from the bytes.
        var declared = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            var other => $"application/octet-stream ({other})"
        };

        var card = await pipeline.ExtractFromImageAsync(bytes, declared, cancellationToken);
        await output.WriteLineAsync(CardJson.Serialize(card));
        return 0;
    }
}
