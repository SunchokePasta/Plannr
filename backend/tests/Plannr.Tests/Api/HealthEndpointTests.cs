using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Plannr.Core.Diagnostics;
using Plannr.Tests.Diagnostics;

namespace Plannr.Tests.Api;

/// <summary>
/// Starts the real Program.cs in memory (no port is opened), so this fails
/// if the pipeline itself is wired wrong -- Serilog, the middleware, the
/// health check. In the ErrorReference collection because Program.cs calls
/// ErrorReference.Configure.
/// </summary>
[Collection(ErrorReferenceCollection.Name)]
public sealed class HealthEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        ErrorReference.Reset();
    }

    [Fact]
    public async Task Health_IsHealthy()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
