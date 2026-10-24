using Microsoft.Extensions.DependencyInjection.Extensions;
using Plannr.Api.Options;
using Plannr.Api.Services;
using Plannr.Services.DependencyInjection;
using Plannr.Services.HandOffs;
using Plannr.Services.Interfaces;
using Plannr.Services.Options;
using Plannr.Services.Services;

namespace Plannr.Api.HandOffs;

public static class HandOffServiceCollectionExtensions
{
    /// <summary>
    /// The API's side of both hand-offs. Needs the repositories
    /// (AddPlannrData, M3) and an INotificationService (M4) registered too.
    /// </summary>
    public static IServiceCollection AddPlannrApiHandOffs(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO(H6.2): register everything the API's side needs, then return services:
        //   - options: configuration.GetValidated<HandOffOptions>(HandOffOptions.SectionName) (a missing or
        //     short secret stops the API starting), registered as a singleton.
        //   - services.TryAddSingleton(TimeProvider.System) ("Try": a test may have registered a fake clock).
        //   - one WakeSignal, as a singleton: the result endpoint rings it and the announcer waits on it,
        //     so both must get the same instance.
        //   - services.AddHostedService<ResultAnnouncer>(): the host starts and stops it.
        //   - the worker's wake client: services.AddPlannrWorkerWakeApi(new Uri(options.WorkerUrl),
        //     options.Secret, TimeSpan.FromSeconds(options.NudgeTimeoutSeconds)).
        //   - IWorkerNudger as WorkerNudger (a singleton: it holds nothing per request).
        //   - ILinkClassifier as LinkClassifier (TryAddSingleton), and CaptureService as scoped: it uses
        //     the scoped repository, and a singleton must never hold a scoped service.
        throw new NotImplementedException("TODO(H6.2): see docs/guides/hand-offs.md");
    }
}
