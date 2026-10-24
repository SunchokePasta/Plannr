using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plannr.Scraping.DependencyInjection;
using Plannr.Services.Interfaces;
using Plannr.Services.Options;
using Plannr.Services.Services;

namespace Plannr.Tests.Extraction;

public sealed class CaptureServiceCollectionExtensionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public async Task AddPlannrCapture_WiresTheWholePipeline()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddPlannrCapture(Config(("Groq:ApiKey", "test-key"), ("Retrieval:BrowserFallbackEnabled", "false")));

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Assert.IsType<ExtractionPipeline>(provider.GetRequiredService<IExtractionPipeline>());
        Assert.IsType<LinkClassifier>(provider.GetRequiredService<ILinkClassifier>());
    }

    [Fact]
    public void EvalsSettings_WaitLongerThanTheDefaults()
    {
        // Eval's own file, from its source folder. Not the copy beside the test
        // DLL: Plannr.Api and Plannr.Eval both copy an "appsettings.json" there,
        // and whichever is copied last wins.
        var backend = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(backend.FullName, "Plannr.slnx")))
        {
            backend = backend.Parent ?? throw new InvalidOperationException("Plannr.slnx not found above the test output.");
        }

        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(backend.FullName, "src", "Plannr.Eval", "appsettings.json"), optional: false)
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Groq:ApiKey", "k")])
            .Build();

        var groq = config.GetValidated<GroqOptions>("Groq");

        Assert.Equal(900, groq.MaxRateLimitWaitSeconds);
        Assert.True(groq.MaxRateLimitWaitSeconds > new GroqOptions().MaxRateLimitWaitSeconds);
    }

    [Fact]
    public void AddPlannrCapture_WithoutAGroqKey_RefusesToRegister()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPlannrCapture(Config()));

        Assert.Contains("Groq:ApiKey", exception.Message);
    }

    [Fact]
    public void AddPlannrCapture_WithABadValue_NamesIt()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPlannrCapture(Config(("Groq:ApiKey", "k"), ("Extraction:FlagBelowConfidence", "3"))));

        Assert.Contains("Extraction:FlagBelowConfidence", exception.Message);
    }
}
