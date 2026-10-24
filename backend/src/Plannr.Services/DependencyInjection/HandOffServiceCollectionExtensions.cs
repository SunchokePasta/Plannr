using Microsoft.Extensions.DependencyInjection;
using Plannr.Services.APIs;
using Plannr.Services.HandOffs;
using Refit;

namespace Plannr.Services.DependencyInjection;

/// <summary>
/// The Refit clients the two programs call each other with (D37). They're
/// registered here because Refit generates their code inside this project;
/// each program calls the one it needs from its own hand-off wiring.
/// </summary>
public static class HandOffServiceCollectionExtensions
{
    /// <summary>The worker's client for the API's /internal endpoints.</summary>
    /// <returns>The client's builder, so a test can swap in a stub handler.</returns>
    public static IHttpClientBuilder AddPlannrInternalApi(
        this IServiceCollection services, Uri apiUrl, string sharedSecret, TimeSpan timeout) =>
        services.AddRefitGeneratedClient<IPlannrInternalApi>()
            .ConfigureHttpClient(client => Configure(client, apiUrl, sharedSecret, timeout));

    /// <summary>The API's client for the worker's wake endpoint.</summary>
    /// <returns>The client's builder, so a test can swap in a stub handler.</returns>
    public static IHttpClientBuilder AddPlannrWorkerWakeApi(
        this IServiceCollection services, Uri workerUrl, string sharedSecret, TimeSpan timeout) =>
        services.AddRefitGeneratedClient<IWorkerWakeApi>()
            .ConfigureHttpClient(client => Configure(client, workerUrl, sharedSecret, timeout));

    private static void Configure(HttpClient client, Uri baseUrl, string sharedSecret, TimeSpan timeout)
    {
        client.BaseAddress = baseUrl;
        client.Timeout = timeout;
        client.DefaultRequestHeaders.Add(SharedSecret.HeaderName, sharedSecret);
    }
}
