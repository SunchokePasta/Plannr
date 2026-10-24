using Microsoft.Extensions.DependencyInjection.Extensions;
using Plannr.Services.DependencyInjection;
using Plannr.Services.HandOffs;
using Plannr.Services.Options;
using Plannr.Worker.Options;

namespace Plannr.Worker.HandOffs;

public static class HandOffServiceCollectionExtensions
{
    /// <summary>The worker's side of both hand-offs. Needs AddPlannrCapture (the pipeline) registered too.</summary>
    public static IServiceCollection AddPlannrWorkerHandOffs(this IServiceCollection services, IConfiguration configuration)
    {
        // TODO(H6.1): register everything the worker's side needs, then return services:
        //   - options: configuration.GetValidated<WorkerOptions>(WorkerOptions.SectionName), as a singleton.
        //   - services.TryAddSingleton(TimeProvider.System).
        //   - one WakeSignal, as a singleton: the wake endpoint and finished jobs ring it, the job loop
        //     waits on it.
        //   - services.AddHostedService<JobLoop>().
        //   - the API's client: services.AddPlannrInternalApi(new Uri(options.ApiUrl), options.Secret,
        //     TimeSpan.FromSeconds(options.ApiTimeoutSeconds)).
        //   - ICaptureJobProcessor as CaptureJobProcessor, transient: the job loop makes one per job, in
        //     that job's own scope.
        throw new NotImplementedException("TODO(H6.1): see docs/guides/hand-offs.md");
    }
}
