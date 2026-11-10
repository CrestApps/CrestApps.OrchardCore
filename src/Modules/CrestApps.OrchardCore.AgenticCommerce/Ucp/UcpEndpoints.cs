using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Orders;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// The UCP REST binding: the store's profile at <c>/.well-known/ucp</c>, checkout sessions and orders.
/// </summary>
public static class UcpEndpoints
{
    /// <summary>
    /// The idempotency rules of UCP: a retry with the same key and body gets the first answer for two days, another
    /// body with the same key gets 409.
    /// </summary>
    public static readonly IdempotencyPolicy Idempotency = new()
    {
        Lifetime = TimeSpan.FromHours(48),
        MaxKeyLength = 255,
        Fingerprint = httpContext => Convert.ToHexStringLower(SHA256.HashData(AgentRequest.Get(httpContext)?.Body ?? [])),
        Conflict = _ => UcpResults.Error(StatusCodes.Status409Conflict, "idempotency_conflict", "This idempotency key was used for another request."),
        InFlight = _ => UcpResults.Error(StatusCodes.Status409Conflict, "idempotency_in_flight", "A request with this idempotency key is still running. Try again in a moment."),
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
            .WithName("UcpProfile");

        routes.MapGet("/" + UcpConstants.ProfilePath + "/{version}", (string version, HttpContext httpContext, UcpProfileBuilder profiles) => ProfileAsync(httpContext, profiles, version))
            .AllowAnonymous()
            .WithName("UcpVersionProfile");

        var group = routes.MapGroup(UcpConstants.RoutePrefix)
            .AllowAnonymous()
            .DisableAntiforgery()
            .AddEndpointFilter<UcpRequestFilter>();

        group.MapPost("checkout-sessions", CreateAsync).WithIdempotency(Idempotency).WithName("UcpCreateCheckout");
        group.MapGet("checkout-sessions/{id}", GetAsync).WithName("UcpGetCheckout");
        group.MapPut("checkout-sessions/{id}", UpdateAsync).WithIdempotency(Idempotency).WithName("UcpUpdateCheckout");
        group.MapPost("checkout-sessions/{id}/complete", CompleteAsync).WithIdempotency(Idempotency).WithName("UcpCompleteCheckout");
        group.MapPost("checkout-sessions/{id}/cancel", CancelAsync).WithIdempotency(Idempotency).WithName("UcpCancelCheckout");
        group.MapGet("orders/{id}", GetOrderAsync).WithName("UcpGetOrder");

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
        var etag = "\"" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))).Substring(0, 32) + "\"";

        httpContext.Response.Headers.CacheControl = "public, max-age=300";
        httpContext.Response.Headers.ETag = etag;

        if (httpContext.Request.Headers.IfNoneMatch.ToString() == etag)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Text(body, "application/json", Encoding.UTF8);
    }

    private static async Task<IResult> CreateAsync(HttpContext httpContext, IAgentCheckoutService checkouts, UcpCheckoutMapper mapper)
    {
        var request = AgentRequest.Get(httpContext);

        if (request.Json is not JsonObject body)
        {
            return UcpResults.Error(StatusCodes.Status400BadRequest, "invalid_request", "Send the checkout as a JSON object.");
        }

        var result = await checkouts.CreateAsync(request.Context, UcpCheckoutMapper.ReadInput(body), httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return await FailAsync(request, result, mapper, httpContext);
        }

        return UcpResults.Json(await mapper.WriteAsync(request, result, httpContext.RequestAborted), StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetAsync(string id, HttpContext httpContext, IAgentCheckoutService checkouts, UcpCheckoutMapper mapper)
    {
        var request = AgentRequest.Get(httpContext);
        var result = await checkouts.GetAsync(request.Context, id, httpContext.RequestAborted);

        return result.Succeeded
            ? UcpResults.Json(await mapper.WriteAsync(request, result, httpContext.RequestAborted))
            : await FailAsync(request, result, mapper, httpContext);
    }

    private static async Task<IResult> UpdateAsync(string id, HttpContext httpContext, IAgentCheckoutService checkouts, UcpCheckoutMapper mapper)
    {
        var request = AgentRequest.Get(httpContext);

        if (request.Json is not JsonObject body)
        {
            return UcpResults.Error(StatusCodes.Status400BadRequest, "invalid_request", "Send the checkout as a JSON object.");
        }

        var result = await checkouts.UpdateAsync(request.Context, id, UcpCheckoutMapper.ReadUpdate(body), httpContext.RequestAborted);

        return result.Succeeded
            ? UcpResults.Json(await mapper.WriteAsync(request, result, httpContext.RequestAborted))
            : await FailAsync(request, result, mapper, httpContext);
    }

    private static async Task<IResult> CompleteAsync(
        string id,
        HttpContext httpContext,
        IAgentCheckoutService checkouts,
        UcpCheckoutMapper mapper,
        IEnumerable<IUcpCheckoutExtension> extensions)
    {
        var request = AgentRequest.Get(httpContext);

        if (request.Json is not JsonObject body)
        {
            return UcpResults.Error(StatusCodes.Status400BadRequest, "invalid_request", "Send the payment as a JSON object.");
        }

        if (UcpCheckoutMapper.ReadPayment(body) is not { } payment)
        {
            return UcpResults.Json(UcpCheckoutMapper.WriteError(request, "Give the payment instrument to complete the checkout with.", AgentMessageCodes.PaymentFailed));
        }

        var current = await checkouts.GetAsync(request.Context, id, httpContext.RequestAborted);

        if (!current.Succeeded)
        {
            return await FailAsync(request, current, mapper, httpContext);
        }

        var context = new UcpCheckoutContext
        {
            Request = request,
            Checkout = current.Checkout,
            View = current.View,
            Json = body,
        };

        foreach (var extension in extensions)
        {
            if (await extension.CompletingAsync(context) is { } refusal)
            {
                current.View.Messages.Add(refusal);

                var refused = await mapper.WriteAsync(request, current, httpContext.RequestAborted);

                refused["ucp"]["status"] = "error";

                return UcpResults.Json(refused);
            }
        }

        var update = UcpCheckoutMapper.ReadUpdate(body);

        update.Lines = null;

        var result = await checkouts.CompleteAsync(request.Context, id, payment, update, httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            return await FailAsync(request, result, mapper, httpContext);
        }

        if (result.View.Status == AgentCheckoutStatus.Completed)
        {
            context.Checkout = result.Checkout;
            context.View = result.View;

            foreach (var extension in extensions)
            {
                await extension.CompletedAsync(context);
            }
        }

        return UcpResults.Json(await mapper.WriteAsync(request, result, httpContext.RequestAborted));
    }

    private static async Task<IResult> CancelAsync(string id, HttpContext httpContext, IAgentCheckoutService checkouts, UcpCheckoutMapper mapper)
    {
        var request = AgentRequest.Get(httpContext);
        var result = await checkouts.CancelAsync(request.Context, id, intentTrace: null, httpContext.RequestAborted);

        return result.Succeeded
            ? UcpResults.Json(await mapper.WriteAsync(request, result, httpContext.RequestAborted))
            : await FailAsync(request, result, mapper, httpContext);
    }

    private static async Task<IResult> GetOrderAsync(
        string id,
        HttpContext httpContext,
        IAgentCheckoutStore checkouts,
        IOrderStore orders,
        AgentOrderViewBuilder views)
    {
        var request = AgentRequest.Get(httpContext);
        var order = await orders.FindByIdAsync(id, httpContext.RequestAborted);
        var checkout = order is null ? null : await checkouts.FindByOrderIdAsync(order.ItemId, httpContext.RequestAborted);

        // A platform reads the orders its agents placed; with a linked account, the buyer's own orders too.
        var mine = checkout is not null && string.Equals(checkout.PlatformId, request.Platform.ItemId, StringComparison.Ordinal);
        var buyers = order is not null && !string.IsNullOrEmpty(request.Context.UserId) && string.Equals(order.UserId, request.Context.UserId, StringComparison.Ordinal);

        if (order is null || (!mine && !buyers))
        {
            return UcpResults.Error(StatusCodes.Status404NotFound, "not_found", "There is no such order.");
        }

        return UcpResults.Json(UcpOrderMapper.Write(await views.BuildAsync(order, checkout, httpContext.RequestAborted), request.Version));
    }

    private static async Task<IResult> FailAsync(AgentRequest request, AgentCheckoutResult result, UcpCheckoutMapper mapper, HttpContext httpContext)
    {
        switch (result.Failure)
        {
            case AgentCheckoutFailure.NotFound:
                return UcpResults.Error(StatusCodes.Status404NotFound, "not_found", result.Error);

            case AgentCheckoutFailure.Busy:
                httpContext.Response.Headers[HeaderNames.RetryAfter] = "1";

                return UcpResults.Error(StatusCodes.Status409Conflict, "conflict", result.Error);

            case AgentCheckoutFailure.TooMany:
                return UcpResults.Error(StatusCodes.Status429TooManyRequests, "rate_limited", result.Error);
        }

        // A business outcome, such as changing a completed checkout, is answered with HTTP 200 and an error status.
        if (result.View is not null && result.Checkout is not null)
        {
            return UcpResults.Json(await mapper.WriteAsync(request, result, httpContext.RequestAborted));
        }

        return UcpResults.Json(UcpCheckoutMapper.WriteError(request, result.Error));
    }
}
