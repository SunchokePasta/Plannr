using Plannr.Services.HandOffs;
using Plannr.Worker.Options;

namespace Plannr.Worker.HandOffs;

/// <summary>
/// The worker's one endpoint: the API's nudge (POST /internal/wake) after
/// it saves a capture. It only rings the job loop's doorbell; the loop
/// then claims the job through the API.
///
/// A minimal-API handler: ASP.NET Core fills each parameter in, the request
/// from the call and the other two from dependency injection.
/// </summary>
public static class WakeEndpoint
{
    public const string Path = "/internal/wake";

    public static IResult Handle(HttpRequest request, WakeSignal signal, WorkerOptions options)
    {
        // TODO(H4.1): the same check as the API's filter (H2.2), then the nudge:
        //   - the secret in request.Headers[SharedSecret.HeaderName] doesn't match options.Secret:
        //     return Results.Unauthorized().
        //   - otherwise signal.Nudge() and return Results.NoContent() (204: done, nothing to send back).
        throw new NotImplementedException("TODO(H4.1): see docs/guides/hand-offs.md");
    }
}
