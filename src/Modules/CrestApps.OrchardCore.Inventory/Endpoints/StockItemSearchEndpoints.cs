using CrestApps.OrchardCore.Inventory.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Inventory.Endpoints;

/// <summary>
/// The search behind the stock item pickers, such as a purchase order's: up to 50 <c>{ value, text }</c> items whose
/// SKU or title starts with the query.
/// </summary>
public static class StockItemSearchEndpoints
{
    public const string RouteName = "CrestApps.Inventory.StockItemSearch";

    private const int MaxResults = 50;

    public static IEndpointRouteBuilder AddStockItemSearchEndpoint(this IEndpointRouteBuilder builder)
    {
        var adminUrlPrefix = builder.ServiceProvider.GetRequiredService<IOptions<AdminOptions>>().Value.AdminUrlPrefix;

        builder.MapGet($"{adminUrlPrefix}/api/crestapps/inventory/stock-items/search", SearchAsync)
            .RequireAuthorization()
            .WithName(RouteName);

        return builder;
    }

    internal static async Task<IResult> SearchAsync(
        string query,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IStockItemStore items)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InventoryPermissions.ManageInventory))
        {
            return Results.Forbid();
        }

        var (_, found) = await items.PageAsync(new StockItemQuery { Search = query?.Trim() }, 0, MaxResults, httpContext.RequestAborted);

        return Results.Ok(found.Select(item => new
        {
            value = item.ItemId,
            text = string.IsNullOrEmpty(item.Sku) ? item.Title : $"{item.Title} · {item.Sku}",
        }));
    }
}
