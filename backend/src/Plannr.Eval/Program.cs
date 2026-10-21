using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plannr.Core.Diagnostics;
using Plannr.Core.Exceptions;
using Plannr.Core.Interfaces;
using Plannr.Eval;
using Plannr.Infrastructure.DependencyInjection;
using Plannr.Infrastructure.Diagnostics;
using Serilog;

// First, before anything can fail: every reference this process mints
// says it came from Plannr.Eval and is in its one log file.
ErrorReference.Configure(ErrorReference.EvalProgram, ErrorReference.MainLog);

// Beside the exe (bin/Debug/net10.0/logs), not the working directory:
// the same place whichever folder Eval is run from. Printed after every
// run so it doesn't have to be remembered.
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

EvalArguments arguments;
try
{
    arguments = EvalArguments.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(EvalArguments.Usage);
    return 2;
}

if (arguments.Command == EvalCommand.Help)
{
    Console.WriteLine(EvalArguments.Usage);
    return 0;
}

// A static logger, created before the host, so a failure while building it
// (a missing Groq key, a bad setting) is logged with a reference too.
var logging = new LoggerConfiguration().UsePlannrDefaults(logDirectory, consoleToStandardError: true);
if (arguments.Verbose)
{
    logging.MinimumLevel.Debug();
}

Log.Logger = logging.CreateLogger();

// Lines written here, outside any class, would otherwise have no SourceContext.
var log = Log.ForContext("SourceContext", "Plannr.Eval");

using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Let the run stop cleanly (and flush its log) instead of being killed.
    e.Cancel = true;
    cancel.Cancel();
};

try
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        ContentRootPath = AppContext.BaseDirectory
    });

    // The host only reads user-secrets in the Development environment; Eval
    // always needs them (the Groq key), whatever environment it runs in.
    builder.Configuration.AddUserSecrets(typeof(EvalArguments).Assembly, optional: true);

    builder.Services.AddSerilog();
    builder.Services.AddPlannrCapture(builder.Configuration);

    await using var services = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

    var pipeline = services.GetRequiredService<IExtractionPipeline>();

    return arguments.Command switch
    {
        EvalCommand.ExtractLink => await ExtractCommands.LinkAsync(pipeline, arguments.Url!, Console.Out, cancel.Token),
        EvalCommand.ExtractImage => await ExtractCommands.ImageAsync(pipeline, arguments.ImagePath!, Console.Out, cancel.Token),
        _ => throw new InvalidOperationException($"{arguments.Command} isn't implemented yet.")
    };
}
catch (OperationCanceledException) when (cancel.IsCancellationRequested)
{
    log.Information("Cancelled.");
    return 130;
}
catch (CaptureException exception)
{
    // An expected outcome for some links (blocked, unsupported, Groq down):
    // a Warning, and the user-facing message first, as the app would show it.
    var reference = ErrorReference.New();
    log.Warning(exception, "[{Reference}] The capture failed: {Detail}", reference, exception.Message);

    Console.Error.WriteLine();
    Console.Error.WriteLine($"Capture failed: {exception.UserMessage}");
    Console.Error.WriteLine($"  Detail:    {exception.Message}");
    Console.Error.WriteLine($"  Reference: {reference} (log: {logDirectory})");
    return 1;
}
catch (Exception exception)
{
    var reference = ErrorReference.New();
    log.Error(exception, "[{Reference}] Plannr.Eval failed.", reference);

    Console.Error.WriteLine();
    Console.Error.WriteLine($"Plannr.Eval failed: {exception.Message}");
    Console.Error.WriteLine($"  Reference: {reference} (log: {logDirectory})");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
