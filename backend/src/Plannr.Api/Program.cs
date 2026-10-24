using Plannr.Api.Middleware;
using Plannr.Services.Diagnostics;
using Plannr.Services.DTOs;
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

// Enums as names, the same as the worker's Refit client writes them (PlannrJson).
builder.Services.AddControllers()
    .AddJsonOptions(json => PlannrJson.Configure(json.JsonSerializerOptions));

// TODO(H6.3): turn the hand-offs on, once M3's AddPlannrData and M4's
// SignalR notifier are registered above it (the wiring needs both):
//   builder.Services.AddPlannrApiHandOffs(builder.Configuration);

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
