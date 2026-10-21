namespace Plannr.Core.Diagnostics;

/// <summary>
/// Short, human-quotable id attached to a single failure. The same value
/// goes into the log line, into the API's error response, and onto the
/// phone's screen -- so a user reading "Reference: 7f3a9c21:02:01" off a
/// failed card is quoting the exact log line that has the stack trace.
///
/// The shape is <c>id:program:log</c>: eight hex characters, then two-digit
/// codes for the program that minted it and the log file in that program the
/// line was written to. A reference passed along from another tier (the API
/// relaying one the Worker stored on a failed capture) keeps the codes it was
/// minted with, because they say where the stack trace is, not who displayed it.
///
/// | Program        | Code | Log file                         | Code |
/// |----------------|------|----------------------------------|------|
/// | Plannr.Api     | 01   | logs/log-yyyyMMdd.txt            | 01   |
/// | Plannr.Worker  | 02   | logs/log-yyyyMMdd.txt            | 01   |
/// | Plannr.Eval    | 03   | logs/log-yyyyMMdd.txt            | 01   |
/// | mobile (Expo)  | 04   | (device console / crash report)  | 01   |
///
/// Unlike Applr, every C# program here is in one solution, so this is the
/// only copy. The mobile app keeps its own TypeScript copy of the table --
/// a new code has to be added there too.
///
/// Each process says which one it is once, at startup, through
/// <see cref="Configure"/>.
/// </summary>
public static class ErrorReference
{
    public const string ApiProgram = "01";
    public const string WorkerProgram = "02";
    public const string EvalProgram = "03";

    /// <summary>Each C# program writes one log file, logs/log-yyyyMMdd.txt.</summary>
    public const string MainLog = "01";

    /// <summary>
    /// What a reference minted before <see cref="Configure"/> ran carries --
    /// visibly not a real program, rather than silently claiming to be one.
    /// </summary>
    private const string Unconfigured = "00:00";

    private static string _suffix = Unconfigured;

    /// <summary>
    /// Called once per process, as early as possible: the first line of each
    /// program's Program.cs.
    /// </summary>
    public static void Configure(string programCode, string logFileCode)
    {
        if (!IsTwoDigits(programCode) || !IsTwoDigits(logFileCode))
        {
            throw new ArgumentException(
                $"Error reference codes are two digits; got program '{programCode}' " +
                $"and log file '{logFileCode}'.");
        }

        _suffix = $"{programCode}:{logFileCode}";
    }

    public static string New() => $"{Guid.NewGuid().ToString("N")[..8]}:{_suffix}";

    /// <summary>
    /// Back to the unconfigured state. Only the tests need this: a real
    /// process configures once and never changes its mind.
    /// </summary>
    internal static void Reset() => _suffix = Unconfigured;

    private static bool IsTwoDigits(string code) =>
        code is { Length: 2 } && char.IsAsciiDigit(code[0]) && char.IsAsciiDigit(code[1]);
}
