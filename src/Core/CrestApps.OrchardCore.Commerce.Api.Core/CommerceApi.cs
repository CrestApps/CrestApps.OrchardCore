using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore;

namespace CrestApps.OrchardCore.Commerce.Api;

/// <summary>
/// The commerce JSON API: every module maps its endpoints into this group, so they share a path, authentication and
/// the conventions Orchard Core's OpenAPI documents describe.
/// </summary>
public static class CommerceApi
{
    /// <summary>
    /// The path every commerce API endpoint starts with. A breaking change adds a <c>v2</c> group beside it.
    /// </summary>
    public const string RoutePrefix = "api/commerce/v1";

    /// <summary>
    /// The header a guest's cart travels in, both ways.
    /// </summary>
    public const string CartTokenHeader = "X-Cart-Token";

    /// <summary>
    /// The header a guest's checkout travels in, both ways.
    /// </summary>
    public const string CheckoutTokenHeader = "X-Checkout-Token";

    /// <summary>
    /// The header that makes a request safe to send again: a second request with the same key gets the first one's
    /// response instead of doing the work twice.
    /// </summary>
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// The scheme API callers authenticate with: a bearer token from the tenant's OpenID server. Session cookies are
    /// never used, so no endpoint needs an antiforgery token.
    /// </summary>
    public const string AuthenticationScheme = OrchardCoreConstants.AuthenticationSchemes.Api;

    /// <summary>
    /// Maps the commerce API's route group. Endpoints require a signed-in caller unless they call
    /// <see cref="AllowGuests{TBuilder}"/>; a guest's cart and checkout travel in headers.
    /// </summary>
    public static RouteGroupBuilder MapCommerceApi(this IEndpointRouteBuilder routes, string tag)
    {
        ArgumentNullException.ThrowIfNull(routes);

        return routes.MapGroup(RoutePrefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AuthenticationScheme })
            .DisableAntiforgery()
            .WithMetadata(GuestSessionTokenHeaderTransport.Instance)
            .WithTags(tag);
    }

    /// <summary>
    /// Lets guests call the endpoint. A bearer token, when sent, still signs the caller in.
    /// </summary>
    public static TBuilder AllowGuests<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.AllowAnonymous();
}
