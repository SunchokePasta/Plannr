using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Plannr.Core.Capture;
using Plannr.Core.Interfaces;
using Plannr.Core.Options;
using Plannr.Infrastructure.Options;

namespace Plannr.Infrastructure.DependencyInjection;

public static class CaptureServiceCollectionExtensions
{
    /// <summary>
    /// The whole extraction pipeline -- retrieval, Groq, normalising -- from
    /// configuration. Binds and validates all three sections here, so a
    /// program that calls this refuses to start with a bad or missing value
    /// (most likely Groq:ApiKey) instead of failing on its first capture.
    /// </summary>
    public static IServiceCollection AddPlannrCapture(this IServiceCollection services, IConfiguration configuration)
    {
        var retrieval = configuration.GetValidated<RetrievalOptions>(RetrievalOptions.SectionName);
        var groq = configuration.GetValidated<GroqOptions>(GroqOptions.SectionName);
        var extraction = configuration.GetValidated<ExtractionOptions>(ExtractionOptions.SectionName);

        return services
            .AddPlannrRetrieval(retrieval)
            .AddPlannrLlm(groq)
            .AddPlannrExtraction(extraction);
    }

    /// <summary>The normaliser and pipeline on their own, for callers that register retrieval and the LLM themselves.</summary>
    public static IServiceCollection AddPlannrExtraction(this IServiceCollection services, ExtractionOptions options)
    {
        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CardNormaliser>();
        services.AddTransient<IExtractionPipeline, ExtractionPipeline>();

        return services;
    }
}
