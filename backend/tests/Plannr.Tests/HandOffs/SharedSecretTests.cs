using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Plannr.Api.HandOffs;
using Plannr.Api.Options;
using Plannr.Services.HandOffs;

namespace Plannr.Tests.HandOffs;

/// <summary>H2.</summary>
public sealed class SharedSecretTests
{
    private const string Secret = HandOffTestData.Secret;

    [Fact(Skip = Todo.H2_1)]
    public void TheRightSecret_Matches() => Assert.True(SharedSecret.Matches(Secret, Secret));

    [Theory(Skip = Todo.H2_1)]
    [InlineData("a-test-secret-that-is-long-enough-1235")]
    [InlineData("a-test-secret")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElse_DoesNot(string? presented) => Assert.False(SharedSecret.Matches(presented, Secret));

    [Fact(Skip = Todo.H2_1)]
    public void AnEmptyExpectedSecret_NeverMatches() => Assert.False(SharedSecret.Matches("", ""));

    private static AuthorizationFilterContext Request(string? header)
    {
        var services = new ServiceCollection()
            .AddSingleton(new HandOffOptions { Secret = Secret })
            .AddSingleton<ILogger<RequireSharedSecretAttribute>>(NullLogger<RequireSharedSecretAttribute>.Instance)
            .BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = services };
        if (header is not null)
        {
            http.Request.Headers[SharedSecret.HeaderName] = header;
        }

        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    }

    [Fact(Skip = Todo.H2_2)]
    public void TheApiFilter_LetsTheRightSecretThrough()
    {
        var context = Request(Secret);

        new RequireSharedSecretAttribute().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Theory(Skip = Todo.H2_2)]
    [InlineData(null)]
    [InlineData("wrong")]
    public void TheApiFilter_RefusesAnythingElse(string? header)
    {
        var context = Request(header);

        new RequireSharedSecretAttribute().OnAuthorization(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }
}
