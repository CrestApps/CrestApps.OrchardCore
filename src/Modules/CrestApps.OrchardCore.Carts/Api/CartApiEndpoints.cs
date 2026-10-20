using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Carts.Api;

/// <summary>
/// The cart API: the caller's cart, priced now. A signed-in caller's cart follows their account; a guest's travels in
/// the <c>X-Cart-Token</c> header, which the response that creates the cart returns.
/// </summary>
internal static class CartApiEndpoints
{
    public const string Tag = "Cart";

    public static IEndpointRouteBuilder MapCartApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(Tag);

        group.MapGet("cart", GetAsync)
            .AllowGuests()
            .WithName("GetCart")
            .WithSummary("Gets the caller's cart, priced now. A caller without one gets an empty cart.");

        group.MapPost("cart/lines", AddAsync)
            .AllowGuests()
            .WithRateLimitGroup(CartConstants.RateLimitGroup)
            .WithName("AddCartLine")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Adds units of a product, or more units to the line that has it, creating the cart when there's none.");

        group.MapPatch("cart/lines/{lineId}", UpdateAsync)
            .AllowGuests()
            .WithRateLimitGroup(CartConstants.RateLimitGroup)
            .WithName("UpdateCartLine")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Changes how many units a line has. Zero removes it.");

        group.MapDelete("cart/lines/{lineId}", RemoveAsync)
            .AllowGuests()
            .WithRateLimitGroup(CartConstants.RateLimitGroup)
            .WithName("RemoveCartLine")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Removes a line.");

        group.MapDelete("cart", ClearAsync)
            .AllowGuests()
            .WithName("DeleteCart")
            .WithSummary("Deletes the caller's cart.");

        group.MapPost("cart/merge", MergeAsync)
            .WithName("MergeGuestCart")
            .WithSummary("Moves the guest cart named by the X-Cart-Token header into the signed-in caller's cart.");

        return routes;
    }

    private static async Task<Ok<ApiCart>> GetAsync(HttpContext httpContext, ICartService cartService)
        => TypedResults.Ok(await PriceAsync(cartService, await cartService.GetCurrentAsync(httpContext.RequestAborted), httpContext));

    private static async Task<Results<Ok<ApiCart>, ValidationProblem, ProblemHttpResult>> AddAsync(
        ApiAddCartLineRequest request,
        HttpContext httpContext,
        ICartService cartService)
    {
        if (string.IsNullOrWhiteSpace(request?.ProductId))
        {
            return ApiProblems.Validation("productId", "Name the product to add.");
        }

        if (request.Quantity is < 1)
        {
            return ApiProblems.Validation("quantity", "Add at least one.");
        }

        var result = await cartService.AddAsync(
            request.ProductId,
            request.PriceId,
            request.VariantId,
            request.Quantity ?? 1,
            request.AddOns ?? new Dictionary<string, string>(),
            request.Details ?? new Dictionary<string, string>(),
            httpContext.RequestAborted);

        return await RespondAsync(result, cartService, httpContext);
    }

    private static async Task<Results<Ok<ApiCart>, ValidationProblem, ProblemHttpResult>> UpdateAsync(
        string lineId,
        ApiUpdateCartLineRequest request,
        HttpContext httpContext,
        ICartService cartService)
    {
        if (request?.Quantity is not { } quantity || quantity < 0)
        {
            return ApiProblems.Validation("quantity", "Give the quantity, or zero to remove the line.");
        }

        if (!await HasLineAsync(cartService, lineId, httpContext))
        {
            return ApiProblems.NotFound();
        }

        var result = quantity == 0
            ? await cartService.RemoveAsync(lineId, httpContext.RequestAborted)
            : await cartService.UpdateQuantityAsync(lineId, quantity, httpContext.RequestAborted);

        return await RespondAsync(result, cartService, httpContext);
    }

    private static async Task<Results<Ok<ApiCart>, ValidationProblem, ProblemHttpResult>> RemoveAsync(
        string lineId,
        HttpContext httpContext,
        ICartService cartService)
    {
        if (!await HasLineAsync(cartService, lineId, httpContext))
        {
            return ApiProblems.NotFound();
        }

        return await RespondAsync(await cartService.RemoveAsync(lineId, httpContext.RequestAborted), cartService, httpContext);
    }

    private static async Task<NoContent> ClearAsync(HttpContext httpContext, ICartService cartService)
    {
        if (await cartService.GetCurrentAsync(httpContext.RequestAborted) is { } cart)
        {
            await cartService.DeleteAsync(cart, httpContext.RequestAborted);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Ok<ApiCart>> MergeAsync(HttpContext httpContext, ICartService cartService)
    {
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            await cartService.MergeGuestCartAsync(userId, httpContext.RequestAborted);
        }

        return TypedResults.Ok(await PriceAsync(cartService, await cartService.GetCurrentAsync(httpContext.RequestAborted), httpContext));
    }

    private static async Task<bool> HasLineAsync(ICartService cartService, string lineId, HttpContext httpContext)
        => await cartService.GetCurrentAsync(httpContext.RequestAborted) is { } cart &&
            cart.Lines.Exists(line => string.Equals(line.LineId, lineId, StringComparison.Ordinal));

    // A change the cart refuses (a product no longer sold, more than there is) is a conflict the client shows as is.
    private static async Task<Results<Ok<ApiCart>, ValidationProblem, ProblemHttpResult>> RespondAsync(CartResult result, ICartService cartService, HttpContext httpContext)
    {
        if (!result.Succeeded)
        {
            return ApiProblems.Conflict(ApiProblems.Codes.NotAvailable, result.Message?.Value);
        }

        return TypedResults.Ok(await PriceAsync(cartService, result.Cart ?? await cartService.GetCurrentAsync(httpContext.RequestAborted), httpContext));
    }

    internal static async Task<ApiCart> PriceAsync(ICartService cartService, Cart cart, HttpContext httpContext)
    {
        if (cart is null)
        {
            return new ApiCart(null, null, [], 0, null, false, null);
        }

        var pricing = await cartService.PriceAsync(cart, httpContext.RequestAborted);
        var currency = pricing.Currency ?? cart.Currency;

        return new ApiCart(
            cart.ItemId,
            currency,
            pricing.Lines.Select(line => new ApiCartLine(
                line.Line.LineId,
                line.Line.ContentItemId,
                line.Line.VariantId,
                line.Line.PriceId,
                line.Title,
                line.Product?.VariantTitle,
                line.Line.Quantity,
                ApiMoney.From(line.UnitPrice, currency),
                ApiMoney.From(line.LineTotal, currency),
                line.AddOns.Select(addOn => new ApiCartLineAddOn(addOn.AddOnId, addOn.Label, addOn.Value, ApiMoney.From(addOn.Price, currency))).ToList(),
                line.MaxQuantity,
                line.IsAvailable,
                line.PriceChanged,
                line.ShipsFromUtc,
                line.Problem?.Value,
                line.Details.Select(detail => new ApiCartLineDetail(detail.Key, detail.Label, detail.Value)).ToList())).ToList(),
            pricing.ItemCount,
            ApiMoney.From(pricing.Subtotal, currency),
            pricing.CanCheckout,
            cart.ExpiresUtc);
    }
}

/// <summary>
/// A product to add to the cart.
/// </summary>
public sealed class ApiAddCartLineRequest
{
    /// <summary>
    /// The product's content item id.
    /// </summary>
    [Required]
    public string ProductId { get; set; }

    /// <summary>
    /// One of the product's prices, or none for its default price.
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// The variant, required for a product sold in variants.
    /// </summary>
    public string VariantId { get; set; }

    /// <summary>
    /// How many to add; one when not given.
    /// </summary>
    public int? Quantity { get; set; }

    /// <summary>
    /// The add-ons chosen, by add-on id: the text typed, or <c>true</c> for a ticked box.
    /// </summary>
    public Dictionary<string, string> AddOns { get; set; }

    /// <summary>
    /// The line's details, by key, such as <c>booking.date</c> for a booking's date.
    /// </summary>
    public Dictionary<string, string> Details { get; set; }
}

/// <summary>
/// A line's new quantity.
/// </summary>
public sealed class ApiUpdateCartLineRequest
{
    /// <summary>
    /// How many units the line has; zero removes it.
    /// </summary>
    [Required]
    public int? Quantity { get; set; }
}

/// <summary>
/// The caller's cart, priced now.
/// </summary>
/// <param name="Id">The cart's id, or null when the caller has none yet.</param>
/// <param name="Currency">The currency it's priced in.</param>
/// <param name="Lines">What's in it.</param>
/// <param name="ItemCount">How many units in all.</param>
/// <param name="Subtotal">The lines' total, before discounts, shipping and tax.</param>
/// <param name="CanCheckout">Whether every line can be bought now.</param>
/// <param name="ExpiresUtc">When an untouched cart is deleted.</param>
public sealed record ApiCart(
    string Id,
    string Currency,
    IReadOnlyList<ApiCartLine> Lines,
    int ItemCount,
    ApiMoney Subtotal,
    bool CanCheckout,
    DateTime? ExpiresUtc);

/// <summary>
/// A line of the cart: a product (a variant of it, at a price, with add-ons) and how many.
/// </summary>
/// <param name="Problem">Why the line can't be bought now, such as the product no longer being sold.</param>
/// <param name="PriceChanged">Whether its price changed since it was added.</param>
/// <param name="ShipsFromUtc">When a preordered or backordered product ships.</param>
public sealed record ApiCartLine(
    string LineId,
    string ProductId,
    string VariantId,
    string PriceId,
    string Title,
    string VariantTitle,
    int Quantity,
    ApiMoney UnitPrice,
    ApiMoney LineTotal,
    IReadOnlyList<ApiCartLineAddOn> AddOns,
    int MaxQuantity,
    bool IsAvailable,
    bool PriceChanged,
    DateTime? ShipsFromUtc,
    string Problem,
    IReadOnlyList<ApiCartLineDetail> Details = null);

/// <summary>
/// One of a line's details, such as a booking's date, as it reads.
/// </summary>
public sealed record ApiCartLineDetail(string Key, string Label, string Value);

/// <summary>
/// An add-on chosen for a line, and what it adds to each unit.
/// </summary>
public sealed record ApiCartLineAddOn(string Id, string Label, string Value, ApiMoney Price);
