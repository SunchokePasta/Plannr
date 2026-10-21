using Serilog;
using Serilog.Events;

namespace Plannr.Infrastructure.Diagnostics;

/// <summary>
/// The one Serilog setup every Plannr program uses (Api, Worker, Eval), so
/// their log lines look the same and an override added here applies to all
/// three. Each program passes its own log folder: the Api and Worker anchor
/// it to their content root, Eval to its own folder, so the file never
/// depends on which directory something happened to be started from.
/// </summary>
public static class PlannrLogging
{
    // SourceContext is what tells you whether a line came from a retriever,
    // the Groq client or EF itself. Without it every line looks the same.
    public const string LogTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

    public const int RetainedLogFiles = 14;

    public static LoggerConfiguration UsePlannrDefaults(
        this LoggerConfiguration configuration,
        string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);

        return configuration
            .MinimumLevel.Information()

            // ASP.NET writes four lines per request; UseSerilogRequestLogging
            // replaces them with one summary line, so the originals are noise.
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)

            // EF logs every generated SQL statement at Information, which
            // buries the lines that matter (Applr's 138KB of SELECTs).
            // Warning still shows command failures; drop it back to
            // Information temporarily when debugging a query.
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)

            // Keep HttpClient's own per-request lines: when a page or Groq
            // is unreachable this is where "Connection refused" is spelled out.
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Information)

            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: LogTemplate)
            .WriteTo.File(
                Path.Combine(logDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedLogFiles,
                outputTemplate: LogTemplate,
                shared: true);
    }
}
