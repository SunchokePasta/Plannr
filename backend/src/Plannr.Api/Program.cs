using Plannr.Api.Middleware;
using Plannr.Core.Diagnostics;
using Plannr.Infrastructure.Diagnostics;
using Serilog;

// First, before anything can fail: every reference this process mints
// says it came from Plannr.Api and is in its one log file.
ErrorReference.Configure(ErrorReference.ApiProgram, ErrorReference.MainLog);

var builder = WebApplication.CreateBuilder(args);

// Anchored to the API's content root rather than the process's working
// directory, so whatever starts it (dotnet run, Docker, ECS) the log file
// ends up in the same place.
builder.Host.UseSerilog((_, configuration) => configuration
    .UsePlannrDefaults(Path.Combine(builder.Environment.ContentRootPath, "logs")));

builder.Services.AddControllers();

// What the AWS load balancer polls. When MySQL arrives (Milestone 3),
// AddDbContextCheck here makes /health report the database too.
builder.Services.AddHealthChecks();

var app = builder.Build();

// First in the pipeline on purpose: anything registered after this is
// wrapped by it, so a throw anywhere downstream becomes a logged
// ApiErrorResponse instead of an unhandled 500 with an empty body.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// One line per request: method, path, status, elapsed ms. Replaces the
// four ASP.NET lines PlannrLogging suppresses.
app.UseSerilogRequestLogging();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Lets the tests' WebApplicationFactory<Program> start this exact pipeline.
public partial class Program;
