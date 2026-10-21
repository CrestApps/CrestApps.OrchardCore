using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Orders.Endpoints;

/// <summary>
/// The searches behind the product and category pickers of the coupon editor, in the shape the item selector reads.
/// </summary>
internal static class CouponItemSearchEndpoints
{
    public const string ProductsPath = "Admin/api/crestapps/orders/coupon-products/search";
    public const string CategoriesPath = "Admin/api/crestapps/orders/coupon-categories/search";

    public static IEndpointRouteBuilder AddCouponItemSearchEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(ProductsPath, SearchProductsAsync).RequireAuthorization();
        builder.MapGet(CategoriesPath, SearchCategoriesAsync).RequireAuthorization();

        return builder;
    }

    private static async Task<IResult> SearchProductsAsync(string query, HttpContext httpContext, IAuthorizationService authorizationService, CouponItemLookup lookup)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CheckoutPermissions.ManageCoupons) &&
            !await authorizationService.AuthorizeAsync(httpContext.User, OrderPermissions.ManageOrders))
        {
            return Results.Forbid();
        }

        var products = await lookup.SearchProductsAsync(query);

        return Results.Ok(products.Select(product => new { value = product.Id, text = product.Text, secondaryText = product.Group }));
    }

    private static async Task<IResult> SearchCategoriesAsync(string query, HttpContext httpContext, IAuthorizationService authorizationService, CouponItemLookup lookup)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CheckoutPermissions.ManageCoupons))
        {
            return Results.Forbid();
        }

        var categories = await lookup.SearchCategoriesAsync(query);

        return Results.Ok(categories.Select(category => new { value = category.Id, text = category.Text, secondaryText = category.Group }));
    }
}
