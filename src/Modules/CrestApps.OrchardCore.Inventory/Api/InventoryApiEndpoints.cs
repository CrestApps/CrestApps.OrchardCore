using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Inventory.Api;

/// <summary>
/// The inventory API: what's in stock at each of the store's locations.
/// </summary>
internal static class InventoryApiEndpoints
{
    public const string Tag = "Inventory";

    public static IEndpointRouteBuilder MapInventoryApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(Tag);

        group.MapGet("products/{id}/availability", GetAvailabilityAsync)
            .AllowGuests()
            .WithName("GetProductAvailability")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets whether a product, or one of its variants, is in stock at each of the store's locations.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiLocationAvailability>>, ProblemHttpResult>> GetAvailabilityAsync(
        string id,
        string variantId,
        HttpContext httpContext,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        IStockItemStore stockItems,
        IInventoryLocationService locations)
    {
        var product = await contentManager.GetAsync(id, VersionOptions.Published);

        if (product is null || !await authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.ViewContent, product))
        {
            return ApiProblems.NotFound();
        }

        var item = (await stockItems.ListByProductAsync(id, httpContext.RequestAborted))
            .FirstOrDefault(stock => !stock.Archived && string.Equals(stock.VariantId ?? string.Empty, variantId ?? string.Empty, StringComparison.Ordinal));
        var answer = new List<ApiLocationAvailability>();

        foreach (var location in await locations.GetLocationsAsync())
        {
            answer.Add(ToAvailability(location, item, item?.FindLevel(location.LocationId)));
        }

        return TypedResults.Ok<IReadOnlyList<ApiLocationAvailability>>(answer);
    }

    // A product nobody counts is in stock everywhere; a location that has none of a counted one is out of it. The count
    // is given only when few are left, the way the product page says "Only 3 left".
    internal static ApiLocationAvailability ToAvailability(InventoryLocation location, StockItem item, StockLocationLevel level)
    {
        if (item is null)
        {
            return new ApiLocationAvailability(location.LocationId, location.Name, location.Address, "inStock", null, true);
        }

        var available = Math.Max(0, level?.Available ?? 0);
        var status = available > 0
            ? available <= item.LowStockThreshold ? "lowStock" : "inStock"
            : item.AllowBackorder ? (item.ReleaseDateUtc.HasValue ? "preorder" : "backorder") : "outOfStock";

        return new ApiLocationAvailability(
            location.LocationId,
            location.Name,
            location.Address,
            status,
            status == "lowStock" ? available : null,
            available > 0 || item.AllowBackorder);
    }
}

/// <summary>
/// Whether a product is in stock at one of the store's locations.
/// </summary>
/// <param name="LocationId">The location's id.</param>
/// <param name="Name">The location's name.</param>
/// <param name="Address">The location's address.</param>
/// <param name="Status"><c>inStock</c>, <c>lowStock</c>, <c>outOfStock</c>, <c>backorder</c> or <c>preorder</c>.</param>
/// <param name="Available">How many are left there, given only when few are.</param>
/// <param name="CanBuy">Whether one can be bought from there now.</param>
public sealed record ApiLocationAvailability(string LocationId, string Name, string Address, string Status, int? Available, bool CanBuy);
