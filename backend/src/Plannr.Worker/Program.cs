using Plannr.Scraping.DependencyInjection;
using Plannr.Services.Diagnostics;
using Plannr.Worker.HandOffs;
using Serilog;

namespace Plannr.Worker;

/// <summary>
/// The worker's entry point. Written out as a class rather than top-level
/// statements like the API's: in a web project those become a public class
/// called Program, and the tests reference both programs, so they couldn't
/// tell the two apart (issue #33).
/// </summary>
internal static class Program
{
    public static void Main(string[] args)
    {
        // First, before anything can fail: every reference this process mints
        // says it came from Plannr.Worker and is in its one log file.
        ErrorReference.Configure(ErrorReference.WorkerProgram, ErrorReference.MainLog);

        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((_, configuration) => configuration
            .UsePlannrDefaults(Path.Combine(builder.Environment.ContentRootPath, "logs")));

        // The extraction pipeline (pages, Chromium, Groq), then the hand-offs:
        // the job loop, the client for the API's /internal endpoints, the signal.
        builder.Services.AddPlannrCapture(builder.Configuration);
        builder.Services.AddPlannrWorkerHandOffs(builder.Configuration);

        var app = builder.Build();

        // The worker's only endpoint: the API's nudge.
        app.MapPost(WakeEndpoint.Path, WakeEndpoint.Handle);

        app.Run();
    }
}
