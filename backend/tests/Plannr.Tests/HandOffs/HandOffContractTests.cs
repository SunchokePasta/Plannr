using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Plannr.Api.Options;
using Plannr.Services.APIs;
using Plannr.Services.DependencyInjection;
using Plannr.Services.Diagnostics;
using Plannr.Services.DTOs;
using Plannr.Services.HandOffs;
using Plannr.Tests.Diagnostics;
using Plannr.Tests.Retrieval;
using Plannr.Worker.Options;

namespace Plannr.Tests.HandOffs;

/// <summary>
/// What the two programs send each other, through the real Refit clients
/// and the JSON settings ASP.NET Core reads requests with. If these pass,
/// a message one program sends is one the other can read.
/// </summary>
/// <remarks>In the ErrorReference collection because starting the API runs its ErrorReference.Configure.</remarks>
[Collection(ErrorReferenceCollection.Name)]
public sealed class HandOffContractTests : IDisposable
{
    /// <summary>The JSON settings the API really reads requests with, taken from its own start-up.</summary>
    private readonly JsonSerializerOptions _apiJson;

    public HandOffContractTests()
    {
        using var api = new WebApplicationFactory<Program>();
        _apiJson = api.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
    }

    public void Dispose() => ErrorReference.Reset();

    private static readonly Uri ApiUrl = new("http://api.test");

    private sealed class Recorded
    {
        public HttpRequestMessage? Request { get; set; }

        public string? Body { get; set; }
    }

    private static IPlannrInternalApi InternalApi(Recorded recorded, HttpResponseMessage answer)
    {
        var stub = new StubHttpHandler(async (request, _) =>
        {
            recorded.Request = request;
            recorded.Body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return answer;
        });

        var services = new ServiceCollection();
        services.AddPlannrInternalApi(ApiUrl, HandOffTestData.Secret, TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(() => stub);

        return services.BuildServiceProvider().GetRequiredService<IPlannrInternalApi>();
    }

    private HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value, _apiJson), Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task AClaim_CarriesTheSecretAndReadsTheApisAnswer()
    {
        var recorded = new Recorded();
        var job = new ClaimedCapture(42, HandOffTestData.Link, 2);

        var claimed = await InternalApi(recorded, Json(job)).ClaimAsync(new ClaimRequest("worker-a"));

        Assert.Equal(job, claimed);
        Assert.Equal("http://api.test/internal/jobs/claim", recorded.Request!.RequestUri!.ToString());
        Assert.Equal(HandOffTestData.Secret, Assert.Single(recorded.Request.Headers.GetValues(SharedSecret.HeaderName)));
        Assert.Equal("worker-a", JsonSerializer.Deserialize<ClaimRequest>(recorded.Body!, _apiJson)!.WorkerId);
    }

    [Fact]
    public async Task A204_IsNoJob()
    {
        var claimed = await InternalApi(new Recorded(), new HttpResponseMessage(HttpStatusCode.NoContent))
            .ClaimAsync(new ClaimRequest("worker-a"));

        Assert.Null(claimed);
    }

    [Fact]
    public async Task AResult_ArrivesWithEveryFieldTheApiNeeds()
    {
        var recorded = new Recorded();
        var card = HandOffTestData.Card() with
        {
            StartsAt = new DateTimeOffset(2026, 12, 14, 19, 0, 0, TimeSpan.Zero),
            StartTimeKnown = true,
            PriceMinPence = 500,
            Currency = "GBP",
            Flags = [new FieldFlag(CardField.EndsAt, FlagReason.Missing)],
            FieldSources = new Dictionary<CardField, FieldSource> { [CardField.Title] = FieldSource.StructuredData }
        };

        await InternalApi(recorded, new HttpResponseMessage(HttpStatusCode.OK)).ReportResultAsync(42, CaptureResult.Succeeded(card));

        Assert.Equal("http://api.test/internal/captures/42/result", recorded.Request!.RequestUri!.ToString());
        var received = JsonSerializer.Deserialize<CaptureResult>(recorded.Body!, _apiJson)!.Card!;
        Assert.Equal(card.StartsAt, received.StartsAt);
        Assert.Equal(card.PriceMinPence, received.PriceMinPence);
        Assert.Equal(card.Flags, received.Flags);
        Assert.Equal(FieldSource.StructuredData, received.FieldSources[CardField.Title]);
    }

    [Fact]
    public async Task AFailure_Arrives()
    {
        var recorded = new Recorded();

        await InternalApi(recorded, new HttpResponseMessage(HttpStatusCode.OK))
            .ReportResultAsync(42, CaptureResult.Failed("That page has gone.", "7f3a9c21:02:01"));

        var received = JsonSerializer.Deserialize<CaptureResult>(recorded.Body!, _apiJson)!;
        Assert.Null(received.Card);
        Assert.Equal(new CaptureFailure("That page has gone.", "7f3a9c21:02:01"), received.Failure);
    }

    [Fact]
    public async Task TheWakeCall_CarriesTheSecret()
    {
        HttpRequestMessage? sent = null;
        var services = new ServiceCollection();
        services.AddPlannrWorkerWakeApi(new Uri("http://worker.test"), HandOffTestData.Secret, TimeSpan.FromSeconds(2))
            .ConfigurePrimaryHttpMessageHandler(() => new StubHttpHandler((request, _) =>
            {
                sent = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }));

        await services.BuildServiceProvider().GetRequiredService<IWorkerWakeApi>().WakeAsync();

        Assert.Equal("http://worker.test/internal/wake", sent!.RequestUri!.ToString());
        Assert.Equal(HandOffTestData.Secret, Assert.Single(sent.Headers.GetValues(SharedSecret.HeaderName)));
    }

    [Fact]
    public void BothPrograms_RefuseAShortSecret()
    {
        Assert.Contains("HandOffs:Secret", Assert.Throws<InvalidOperationException>(() => new HandOffOptions { Secret = "short" }.Validate()).Message);
        Assert.Contains("Worker:Secret", Assert.Throws<InvalidOperationException>(() => new WorkerOptions { Secret = "short" }.Validate()).Message);
    }

    [Fact]
    public void TheDefaults_AreValidOnceASecretIsSet()
    {
        new HandOffOptions { Secret = HandOffTestData.Secret }.Validate();
        new WorkerOptions { Secret = HandOffTestData.Secret }.Validate();
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://api.test")]
    public void ABadUrl_IsRefusedByName(string url)
    {
        Assert.Contains("HandOffs:WorkerUrl", Assert.Throws<InvalidOperationException>(() =>
            new HandOffOptions { Secret = HandOffTestData.Secret, WorkerUrl = url }.Validate()).Message);
        Assert.Contains("Worker:ApiUrl", Assert.Throws<InvalidOperationException>(() =>
            new WorkerOptions { Secret = HandOffTestData.Secret, ApiUrl = url }.Validate()).Message);
    }

    [Fact]
    public void ZeroSlots_IsRefused() =>
        Assert.Contains("Worker:Slots", Assert.Throws<InvalidOperationException>(() =>
            new WorkerOptions { Secret = HandOffTestData.Secret, Slots = 0 }.Validate()).Message);
}
