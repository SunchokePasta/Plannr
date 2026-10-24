using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Plannr.Core.Extraction;
using Plannr.Core.Interfaces;
using Plannr.Core.Options;

namespace Plannr.Infrastructure.DependencyInjection;

public static class ExtractionServiceCollectionExtensions
{
    /// <summary>
    /// The classifier, normaliser and pipeline on their own, for callers that
    /// register retrieval and the LLM themselves. AddPlannrCapture (in
    /// Plannr.Scraping) calls this after registering both.
    /// </summary>
    public static IServiceCollection AddPlannrExtraction(this IServiceCollection services, ExtractionOptions options)
    {
        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ILinkClassifier, LinkClassifier>();
        services.AddSingleton<CardNormaliser>();
        services.AddTransient<IExtractionPipeline, ExtractionPipeline>();

        return services;
    }
}
