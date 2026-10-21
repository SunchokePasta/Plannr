using Microsoft.Extensions.DependencyInjection;
using Plannr.Core.Interfaces;
using Plannr.Infrastructure.DependencyInjection;
using Plannr.Infrastructure.Llm;
using Plannr.Infrastructure.Options;

namespace Plannr.Tests.Llm;

public sealed class LlmServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPlannrLlm_WiresTheExtractorAndGroqClient()
    {
        var services = new ServiceCollection().AddLogging().AddPlannrLlm(new GroqOptions { ApiKey = "test-key" });

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Assert.IsType<GroqLlmExtractor>(provider.GetRequiredService<ILlmExtractor>());
        Assert.NotNull(provider.GetRequiredService<IGroqApi>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public async Task TheRealRefitClient_SendsWhatGroqExpects()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var stub = new Plannr.Tests.Retrieval.StubHttpHandler(async (request, _) =>
        {
            sent = request;
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"model":"qwen/qwen3.8-27b","choices":[{"message":{"content":"{\"title\":{\"value\":\"Market\",\"confidence\":0.9}}"},"finish_reason":"stop"}],
                     "usage":{"prompt_tokens":10,"completion_tokens":5}}
                    """, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var services = new ServiceCollection()
            .AddLogging()
            .AddPlannrLlm(
                new GroqOptions { ApiKey = "test-key" },
                client => client.ConfigurePrimaryHttpMessageHandler(() => stub));
        using var provider = services.BuildServiceProvider();

        var fields = await provider.GetRequiredService<ILlmExtractor>().ExtractAsync(new Plannr.Core.Capture.RetrievedContent
        {
            SourceUrl = new Uri("https://venue.example.com/e/1"),
            SourceType = Plannr.Core.Capture.SourceType.WebPage,
            BodyText = "Market, Sat"
        });

        Assert.Equal("Market", fields.Title!.Value);
        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", sent!.RequestUri!.ToString());
        Assert.Equal("Bearer test-key", sent.Headers.Authorization!.ToString());
        Assert.Contains("\"response_format\":{\"type\":\"json_object\"}", body);
        Assert.Contains("\"role\":\"system\",\"content\":\"", body);
        Assert.Contains("\"reasoning_effort\":\"none\"", body);
    }

    [Fact]
    public void AddPlannrLlm_KeepsATimeProviderRegisteredFirst()
    {
        var pinned = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(pinned)
            .AddPlannrLlm(new GroqOptions { ApiKey = "test-key" });

        using var provider = services.BuildServiceProvider();

        Assert.Same(pinned, provider.GetRequiredService<TimeProvider>());
    }
}
