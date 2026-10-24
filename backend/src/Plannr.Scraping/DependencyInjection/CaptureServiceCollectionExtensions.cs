using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plannr.Scraping.Options;
using Plannr.Services.DependencyInjection;
using Plannr.Services.Options;

namespace Plannr.Scraping.DependencyInjection;

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
}
