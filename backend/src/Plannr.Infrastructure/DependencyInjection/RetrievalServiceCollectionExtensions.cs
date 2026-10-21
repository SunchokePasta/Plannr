using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Plannr.Core.Capture;
using Plannr.Core.Interfaces;
using Plannr.Infrastructure.Options;
using Plannr.Infrastructure.Retrieval;

namespace Plannr.Infrastructure.DependencyInjection;

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
        services.AddSingleton<ILinkClassifier, LinkClassifier>();
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

        services.AddTransient<IContentRetriever, WebPageRetriever>();

        return services;
    }
}
