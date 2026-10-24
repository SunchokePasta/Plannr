using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Plannr.Api.Options;
using Plannr.Services.HandOffs;

namespace Plannr.Api.HandOffs;

/// <summary>
/// Refuses (401) any request to the controller or action it's on that
/// doesn't carry the shared secret in <see cref="SharedSecret.HeaderName"/>.
/// Only the worker knows the secret, so only the worker gets in.
///
/// An authorization filter runs before model binding and before the action:
/// setting <c>context.Result</c> here answers the request and the action
/// never runs.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireSharedSecretAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // TODO(H2.2): compare what the request carries with what the API is configured with:
        //   - expected: HandOffOptions.Secret. An attribute can't take constructor injection, so ask
        //     the request's services: context.HttpContext.RequestServices.GetRequiredService<HandOffOptions>().
        //   - presented: context.HttpContext.Request.Headers[SharedSecret.HeaderName].ToString().
        //   - no match (SharedSecret.Matches): log a warning naming the path (never the values), and set
        //     context.Result = new UnauthorizedResult().
        throw new NotImplementedException("TODO(H2.2): see docs/guides/hand-offs.md");
    }
}
