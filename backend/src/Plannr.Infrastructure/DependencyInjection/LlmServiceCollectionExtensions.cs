using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Plannr.Core.Interfaces;
using Plannr.Infrastructure.Llm;
using Plannr.Infrastructure.Options;
using Refit;

namespace Plannr.Infrastructure.DependencyInjection;

public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// The Groq client and extractor, with options the program has already
    /// bound through GetValidated (so a missing key has already stopped it).
    /// Groq is a fixed, trusted host, so its client doesn't go through
    /// PublicAddressGuard the way shared links do.
    /// </summary>
    /// <param name="configureClient">
    /// Extra setup for Groq's HttpClient, applied last. Tests use it to swap
    /// in a stub handler -- the global ConfigureHttpClientDefaults doesn't
    /// reach this client, because Refit sets its own primary handler.
    /// </param>
    public static IServiceCollection AddPlannrLlm(
        this IServiceCollection services,
        GroqOptions options,
        Action<IHttpClientBuilder>? configureClient = null)
    {
        services.AddSingleton(options);

        // Tests and anything else that needs a pinned "now" register their
        // own TimeProvider first; this only fills the gap.
        services.TryAddSingleton(TimeProvider.System);

        // Refit 16's source-generated client: the request code is written at
        // build time, so nothing is built by reflection at runtime. (Applr's
        // older Refit used AddRefitClient, which in 16 needs the separate
        // Refit.Reflection package.)
        var client = services
            .AddRefitGeneratedClient<IGroqApi>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

        configureClient?.Invoke(client);

        services.AddTransient<ILlmExtractor, GroqLlmExtractor>();

        return services;
    }
}
