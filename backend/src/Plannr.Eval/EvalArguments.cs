namespace Plannr.Eval;

public enum EvalCommand
{
    Help,
    ExtractLink,
    ExtractImage,
    Models
}

/// <summary>
/// The command line, parsed by hand: there are only three commands, and a
/// parsing library would be more to learn than the parser it replaces.
/// </summary>
public sealed record EvalArguments(EvalCommand Command, Uri? Url = null, string? ImagePath = null, bool Verbose = false)
{
    public const string Usage = """
        Plannr.Eval: run Plannr's extraction from the command line.

          extract <url>             Fetch a shared link and print its card as JSON.
          extract --image <path>    Read a screenshot (PNG, JPEG or WebP) and print its card.
          models                    List the Groq models your key can use.
          help                      Show this.

        Options:
          --verbose                 Log at Debug, including the full prompt sent to Groq.

        The card goes to stdout and the log to stderr, so "extract <url> > card.json" keeps just the card.
        Set the Groq key once with: dotnet user-secrets set "Groq:ApiKey" "gsk_..." (from src/Plannr.Eval; the key itself, no < >).
        """;

    /// <exception cref="ArgumentException">The arguments don't form a command; the message says why.</exception>
    public static EvalArguments Parse(IReadOnlyList<string> args)
    {
        var verbose = args.Contains("--verbose", StringComparer.OrdinalIgnoreCase);
        var rest = args.Where(a => !a.Equals("--verbose", StringComparison.OrdinalIgnoreCase)).ToList();

        if (rest.Count == 0 || rest[0] is "help" or "--help" or "-h")
        {
            return new EvalArguments(EvalCommand.Help, Verbose: verbose);
        }

        switch (rest[0].ToLowerInvariant())
        {
            case "models" when rest.Count == 1:
                return new EvalArguments(EvalCommand.Models, Verbose: verbose);

            case "extract" when rest.Count == 3 && rest[1] == "--image":
                return new EvalArguments(EvalCommand.ExtractImage, ImagePath: rest[2], Verbose: verbose);

            case "extract" when rest.Count == 2:
                if (!Uri.TryCreate(rest[1], UriKind.Absolute, out var url))
                {
                    throw new ArgumentException($"'{rest[1]}' isn't an absolute URL (include https://).");
                }

                return new EvalArguments(EvalCommand.ExtractLink, Url: url, Verbose: verbose);

            default:
                throw new ArgumentException($"Unrecognised command: {string.Join(' ', rest)}");
        }
    }
}
