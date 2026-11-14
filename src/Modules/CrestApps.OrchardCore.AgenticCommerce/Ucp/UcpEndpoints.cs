using OrchardCore.RateLimits;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// The UCP REST binding: the store's profile at <c>/.well-known/ucp</c>, checkout sessions and orders. The operations
/// themselves are in <see cref="UcpOperations"/>, which the other bindings share.
/// </summary>
public static class UcpEndpoints
{
    /// <summary>
    /// The error code of an idempotency key used for another request.
    /// </summary>
    public const string IdempotencyConflictCode = "idempotency_conflict";

    /// <summary>
    /// The error code of a request whose idempotency key is still in use by a running request.
    /// </summary>
    public const string IdempotencyInFlightCode = "idempotency_in_flight";

    /// <summary>
    /// The idempotency rules of UCP: every POST and PUT carries a key (UCP's OpenAPI requires it, and a request
    /// signature without a created time relies on it against replays); a retry with the same key and body gets the
    /// first answer for two days, another body with the same key gets 409.
    /// </summary>
    public static readonly IdempotencyPolicy Idempotency = new()
    {
        Lifetime = TimeSpan.FromHours(48),
        MaxKeyLength = 255,
        Fingerprint = httpContext => Convert.ToHexStringLower(SHA256.HashData(AgentRequest.Get(httpContext)?.Body ?? [])),
        MissingKey = _ => UcpResults.Error(StatusCodes.Status400BadRequest, "invalid_request", "Send an Idempotency-Key header with every POST and PUT."),
        Conflict = _ => UcpResults.Error(StatusCodes.Status409Conflict, IdempotencyConflictCode, "This idempotency key was used for another request."),
        InFlight = _ => UcpResults.Error(StatusCodes.Status409Conflict, IdempotencyInFlightCode, "A request with this idempotency key is still running. Try again in a moment."),
        InvalidKey = _ => UcpResults.Error(StatusCodes.Status400BadRequest, "invalid_request", "An idempotency key is at most 255 characters."),
    };

    /// <summary>
    /// Maps the UCP endpoints.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    public static IEndpointRouteBuilder MapUcp(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/" + UcpConstants.ProfilePath, (HttpContext httpContext, UcpProfileBuilder profiles) => ProfileAsync(httpContext, profiles, UcpConstants.Versions[0]))
            .AllowAnonymous()
            .WithName("UcpProfile").WithRateLimitGroup(AgenticCommerceConstants.RateLimitGroups.Checkout);

        routes.MapGet("/" + UcpConstants.ProfilePath + "/{version}", (string version, HttpContext httpContext, UcpProfileBuilder profiles) => ProfileAsync(httpContext, profiles, version))
            .AllowAnonymous()
            .WithName("UcpVersionProfile").WithRateLimitGroup(AgenticCommerceConstants.RateLimitGroups.Checkout);

        var group = routes.MapGroup(UcpConstants.RoutePrefix)
            .AllowAnonymous()
            .DisableAntiforgery()
            .AddEndpointFilter<UcpRequestFilter>();

        group.MapPost("checkout-sessions", CreateAsync).WithIdempotency(Idempotency).WithName("UcpCreateCheckout").WithRateLimitGroup(AgenticCommerceConstants.RateLimitGroups.Checkout);
        group.MapGet("checkout-sessions/{id}", GetAsync).WithName("UcpGetCheckout").WithRateLimitGroup(AgenticCommerceConstants.RateLimitGroups.Checkout);
        group.MapPut("checkout-sessions/{id}", UpdateAsync).WithIdempotency(Idempotency).WithName("UcpUpdateCheckout").WithRateLimitGroup(AgenticCommerceConstants.RateLimitGroups.Checkout);
        group.MapPost("checkout-sessions/{id}/complete", CompleteAsync).WithIdempotency(Idempotency).WithName("UcpCompleteCheckout").WithRateLimitGroup(AgenticCommerceConstants.RateLimitGroups.CheckoutComplete);
        group.MapPost("checkout-sessions/{id}/cancel", CancelAsync).WithIdempotency(Idempotency).WithName("UcpCancelCheckout").WithRateLimitGroup(AgenticCommerceConstants.RateLimitGroups.Checkout);
        group.MapGet("orders/{id}", GetOrderAsync).WithName("UcpGetOrder").WithRateLimitGroup(AgenticCommerceConstants.RateLimitGroups.Checkout);

        return routes;
    }

    // The profile is public and cached; it is never redirected.
    private static async Task<IResult> ProfileAsync(HttpContext httpContext, UcpProfileBuilder profiles, string version)
    {
        if (!UcpConstants.Versions.Contains(version, StringComparer.Ordinal))
        {
            return Results.NotFound();
        }

        var body = (await profiles.BuildAsync(version, httpContext.RequestAborted)).ToJsonString(JsonSerializerOptions.Web);
        var etag = $"\"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)).AsSpan(0, 16))}\"";

        httpContext.Response.Headers.CacheControl = "public, max-age=300";
        httpContext.Response.Headers.ETag = etag;

        if (httpContext.Request.Headers.IfNoneMatch.ToString() == etag)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Text(body, "application/json", Encoding.UTF8);
    }

    private static async Task<IResult> CreateAsync(HttpContext httpContext, UcpOperations operations)
    {
        var request = AgentRequest.Get(httpContext);

        return (await operations.CreateCheckoutAsync(request, request.Json, httpContext.RequestAborted)).ToHttpResult(httpContext);
    }

    private static async Task<IResult> GetAsync(string id, HttpContext httpContext, UcpOperations operations)
        => (await operations.GetCheckoutAsync(AgentRequest.Get(httpContext), id, httpContext.RequestAborted)).ToHttpResult(httpContext);

    private static async Task<IResult> UpdateAsync(string id, HttpContext httpContext, UcpOperations operations)
    {
        var request = AgentRequest.Get(httpContext);

        return (await operations.UpdateCheckoutAsync(request, id, request.Json, httpContext.RequestAborted)).ToHttpResult(httpContext);
    }

    private static async Task<IResult> CompleteAsync(string id, HttpContext httpContext, UcpOperations operations)
    {
        var request = AgentRequest.Get(httpContext);

        return (await operations.CompleteCheckoutAsync(request, id, request.Json, httpContext.RequestAborted)).ToHttpResult(httpContext);
    }

    private static async Task<IResult> CancelAsync(string id, HttpContext httpContext, UcpOperations operations)
        => (await operations.CancelCheckoutAsync(AgentRequest.Get(httpContext), id, httpContext.RequestAborted)).ToHttpResult(httpContext);

    private static async Task<IResult> GetOrderAsync(string id, HttpContext httpContext, UcpOperations operations)
        => (await operations.GetOrderAsync(AgentRequest.Get(httpContext), id, httpContext.RequestAborted)).ToHttpResult(httpContext);
}
