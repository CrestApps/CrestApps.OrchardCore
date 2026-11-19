using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore;

namespace CrestApps.OrchardCore.Api;

/// <summary>
/// The conventions every module's JSON API follows: a versioned route group, Orchard Core's API authentication
/// scheme, no antiforgery (callers send a token, not a cookie), problem responses from <see cref="ApiProblems"/> and
/// idempotent writes through <see cref="IdempotencyEndpointExtensions.WithIdempotency(RouteHandlerBuilder)"/>.
/// </summary>
public static class ModuleApi
{
    /// <summary>
    /// The header a caller sends a key in to make a write safe to send again.
    /// </summary>
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// The authentication scheme of the API, which accepts bearer tokens (not the site's cookie).
    /// </summary>
    public const string AuthenticationScheme = OrchardCoreConstants.AuthenticationSchemes.Api;

    /// <summary>
    /// Maps a module's API group, such as <c>api/tickets/v1</c>: signed in through the API scheme, without antiforgery,
    /// and shown under the tag in the API documentation.
    /// </summary>
    /// <param name="routes">The routes.</param>
    /// <param name="prefix">The group's route prefix, such as <c>api/tickets/v1</c>.</param>
    /// <param name="tag">The tag the group's endpoints are listed under.</param>
    /// <returns>The group.</returns>
    public static RouteGroupBuilder MapModuleApi(this IEndpointRouteBuilder routes, string prefix, string tag)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        return routes.MapGroup(prefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AuthenticationScheme })
            .DisableAntiforgery()
            .WithTags(tag);
    }
}
