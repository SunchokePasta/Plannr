using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plannr.Api.HandOffs;
using Plannr.Api.Notifications;
using Plannr.Api.Services;
using Plannr.Data.Repositories;
using Plannr.Scraping.DependencyInjection;
using Plannr.Worker.HandOffs;

namespace Plannr.Tests.HandOffs;

/// <summary>H6: each program's dependency-injection set-up builds, with every lifetime allowed.</summary>
public sealed class HandOffWiringTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    /// <summary>Catches a missing registration, and a short-lived service held by a long-lived one.</summary>
    private static readonly ServiceProviderOptions Strict = new() { ValidateOnBuild = true, ValidateScopes = true };

    [Fact(Skip = Todo.H6_1)]
    public async Task TheWorker_WiresTheJobLoopAndProcessor()
    {
        var config = Config(("Worker:Secret", HandOffTestData.Secret), ("Groq:ApiKey", "test-key"), ("Retrieval:BrowserFallbackEnabled", "false"));
        var services = new ServiceCollection().AddLogging();
        services.AddPlannrCapture(config);
        services.AddPlannrWorkerHandOffs(config);

        await using var provider = services.BuildServiceProvider(Strict);

        Assert.Contains(provider.GetServices<IHostedService>(), s => s is JobLoop);
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<CaptureJobProcessor>(scope.ServiceProvider.GetRequiredService<ICaptureJobProcessor>());
    }

    [Fact(Skip = Todo.H6_2)]
    public async Task TheApi_WiresTheAnnouncerNudgerAndCaptureService()
    {
        var config = Config(("HandOffs:Secret", HandOffTestData.Secret));
        var services = new ServiceCollection().AddLogging();

        // Until M3 (AddPlannrData) and M4 (the SignalR notifier), stand-ins for what the API's own wiring needs.
        services.AddScoped<ICaptureRepository, FakeCaptureRepository>();
        services.AddScoped<ICardRepository, FakeCardRepository>();
        services.AddScoped<INotificationService, FakeNotificationService>();
        services.AddPlannrApiHandOffs(config);

        await using var provider = services.BuildServiceProvider(Strict);

        Assert.Contains(provider.GetServices<IHostedService>(), s => s is ResultAnnouncer);
        Assert.IsType<WorkerNudger>(provider.GetRequiredService<IWorkerNudger>());
        await using var scope = provider.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CaptureService>());
    }

    [Fact(Skip = Todo.H6_1)]
    public void WithoutASecret_TheWorkerDoesntStart() =>
        Assert.Contains("Worker:Secret", Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPlannrWorkerHandOffs(Config())).Message);

    [Fact(Skip = Todo.H6_2)]
    public void WithoutASecret_TheApiDoesntStart() =>
        Assert.Contains("HandOffs:Secret", Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPlannrApiHandOffs(Config())).Message);
}
