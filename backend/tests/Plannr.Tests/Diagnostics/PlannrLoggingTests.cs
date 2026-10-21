using Plannr.Infrastructure.Diagnostics;
using Serilog;

namespace Plannr.Tests.Diagnostics;

public sealed class PlannrLoggingTests : IDisposable
{
    private readonly string _logDirectory =
        Path.Combine(Path.GetTempPath(), "plannr-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_logDirectory))
        {
            Directory.Delete(_logDirectory, recursive: true);
        }
    }

    [Fact]
    public void UsePlannrDefaults_WritesTheTemplateToADailyFile()
    {
        using (var logger = new LoggerConfiguration().UsePlannrDefaults(_logDirectory).CreateLogger())
        {
            logger.ForContext("SourceContext", "Plannr.Tests")
                .Information("Capture {CaptureId} done.", 42);
        }

        var file = Assert.Single(Directory.GetFiles(_logDirectory, "log-*.txt"));
        var line = File.ReadAllText(file);

        Assert.Contains("[INF] Plannr.Tests Capture 42 done.", line);
    }

    [Fact]
    public void UsePlannrDefaults_HidesAspNetCoreInformation()
    {
        using (var logger = new LoggerConfiguration().UsePlannrDefaults(_logDirectory).CreateLogger())
        {
            logger.ForContext("SourceContext", "Microsoft.AspNetCore.Hosting.Diagnostics")
                .Information("Request starting");
            logger.ForContext("SourceContext", "Microsoft.AspNetCore.Hosting.Diagnostics")
                .Warning("Something worth seeing");
        }

        var text = File.ReadAllText(Assert.Single(Directory.GetFiles(_logDirectory, "log-*.txt")));

        Assert.DoesNotContain("Request starting", text);
        Assert.Contains("Something worth seeing", text);
    }
}
