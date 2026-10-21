using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Orders.Endpoints;

/// <summary>
/// The searches behind the product and category pickers of the coupon editor, in the shape the item selector reads.
/// </summary>
internal static class CouponItemSearchEndpoints
{
    public const string ProductsRouteName = "CrestApps.Orders.CouponProductSearch";
    public const string CategoriesRouteName = "CrestApps.Orders.CouponCategorySearch";

    public static IEndpointRouteBuilder AddCouponItemSearchEndpoints(this IEndpointRouteBuilder builder)
    {
        var adminUrlPrefix = builder.ServiceProvider.GetRequiredService<IOptions<AdminOptions>>().Value.AdminUrlPrefix;

        builder.MapGet($"{adminUrlPrefix}/api/crestapps/orders/coupon-products/search", SearchProductsAsync)
            .RequireAuthorization()
            .WithName(ProductsRouteName);

        builder.MapGet($"{adminUrlPrefix}/api/crestapps/orders/coupon-categories/search", SearchCategoriesAsync)
            .RequireAuthorization()
            .WithName(CategoriesRouteName);

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
