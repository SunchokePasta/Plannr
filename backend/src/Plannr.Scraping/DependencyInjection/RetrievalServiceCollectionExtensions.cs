using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Plannr.Scraping.Options;
using Plannr.Scraping.Retrieval;
using Plannr.Services.Interfaces;

namespace Plannr.Scraping.DependencyInjection;

public static class RetrievalServiceCollectionExtensions
{
    /// <summary>
    /// Everything that turns a shared link into RetrievedContent. Called by
    /// each program that runs the pipeline (Eval now, the Worker later) with
    /// options it has already bound through GetValidated.
    /// </summary>
    public static IServiceCollection AddPlannrRetrieval(this IServiceCollection services, RetrievalOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<HtmlContentParser>();

        services
            .AddHttpClient<IPageFetcher, HttpPageFetcher>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                ConnectCallback = PublicAddressGuard.ConnectAsync,

                // A proxy would make the guard check the proxy's address
                // instead of the site's. Plannr never needs one.
                UseProxy = false,

                AllowAutoRedirect = true,
                MaxAutomaticRedirections = options.MaxRedirects,
                AutomaticDecompression = DecompressionMethods.All,

                // Re-resolve DNS now and then on long-lived pooled connections.
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            });

        // One shared browser for the whole process (it's the expensive part);
        // the container disposes it, closing Chromium, on shutdown.
        if (options.BrowserFallbackEnabled)
        {
            services.AddSingleton<IBrowserPageFetcher, PlaywrightPageFetcher>();
        }

        services.AddTransient<IContentRetriever, WebPageRetriever>();

        return services;
    }
}
