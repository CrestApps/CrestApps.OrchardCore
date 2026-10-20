using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Api;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Orders.Api;

/// <summary>
/// Checking out the caller's cart over the API, and the gift cards that pay toward a checkout.
/// </summary>
internal static class OrderCheckoutApiEndpoints
{
    public static IEndpointRouteBuilder MapOrderCheckoutApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(CheckoutApi.Tag);

        group.MapPost("checkout", StartAsync)
            .AllowGuests()
            .WithIdempotency()
            .WithRateLimitGroup(CartConstants.RateLimitGroup)
            .WithName("StartCheckout")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Checks out the caller's cart: places an order for what's in it, holding its stock, and starts the order's checkout. A guest's checkout comes back in the X-Checkout-Token header.");

        return routes;
    }

    public static IEndpointRouteBuilder MapGiftCardCheckoutApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(CheckoutApi.Tag);

        group.MapPost("checkout/{id}/gift-cards", ApplyGiftCardAsync)
            .AllowGuests()
            .WithRateLimitGroup(CheckoutConstants.RateLimitGroups.Payment)
            .WithName("ApplyCheckoutGiftCard")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Puts a gift card toward the checkout, by its code.");

        group.MapDelete("checkout/{id}/gift-cards/{giftCardId}", RemoveGiftCardAsync)
            .AllowGuests()
            .WithName("RemoveCheckoutGiftCard")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Takes a gift card off the checkout. The gift card's id is the reference of its tender.");

        return routes;
    }

    private static async Task<Results<Created<ApiCheckout>, ProblemHttpResult>> StartAsync(
        HttpContext httpContext,
        CartCheckoutService cartCheckout)
    {
        var result = await cartCheckout.StartAsync(httpContext.User, httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            var code = result.Failure switch
            {
                CartCheckoutFailure.EmptyCart => ApiProblems.Codes.EmptyCart,
                CartCheckoutFailure.OutOfStock => ApiProblems.Codes.OutOfStock,
                CartCheckoutFailure.Concurrency => ApiProblems.Codes.Concurrency,
                _ => ApiProblems.Codes.NotAvailable,
            };

            return ApiProblems.Conflict(code, string.Join(" ", result.Problems));
        }

        return TypedResults.Created($"{httpContext.Request.PathBase}/{CommerceApi.RoutePrefix}/checkout/{result.Session.SessionId}", CheckoutApi.Read(result.Session));
    }

    private static async Task<Results<Ok<ApiCheckout>, ValidationProblem, ProblemHttpResult>> ApplyGiftCardAsync(
        string id,
        ApiGiftCardRequest request,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        GiftCardCheckoutService giftCards)
    {
        if (string.IsNullOrWhiteSpace(request?.Code))
        {
            return ApiProblems.Validation("code", "Enter the gift card's code.");
        }

        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        var flow = new CheckoutFlow(session);

        if (!GiftCardCheckoutService.AcceptsGiftCards(flow))
        {
            return ApiProblems.Conflict(ApiProblems.Codes.Unsupported, "Gift cards can't pay for this checkout.");
        }

        if (await giftCards.ApplyAsync(flow, request.Code.Trim(), httpContext.Connection?.RemoteIpAddress?.ToString() ?? "unknown") is { } error)
        {
            return ApiProblems.Validation("code", error);
        }

        await store.SaveAsync(session, httpContext.RequestAborted);

        return TypedResults.Ok(CheckoutApi.Read(session));
    }

    private static async Task<Results<Ok<ApiCheckout>, ProblemHttpResult>> RemoveGiftCardAsync(
        string id,
        string giftCardId,
        HttpContext httpContext,
        ICheckoutSessionStore store,
        GiftCardCheckoutService giftCards)
    {
        if (await CheckoutApi.FindOpenAsync(store, id, httpContext.RequestAborted) is not { } session)
        {
            return ApiProblems.NotFound();
        }

        if (await giftCards.RemoveAsync(new CheckoutFlow(session), giftCardId))
        {
            await store.SaveAsync(session, httpContext.RequestAborted);
        }

        return TypedResults.Ok(CheckoutApi.Read(session));
    }
}

/// <summary>
/// A gift card to pay toward the checkout.
/// </summary>
public sealed class ApiGiftCardRequest
{
    /// <summary>
    /// The code printed on the gift card or sent in its email.
    /// </summary>
    public string Code { get; set; }
}
