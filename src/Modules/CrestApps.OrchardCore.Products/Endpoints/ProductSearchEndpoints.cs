using CrestApps.OrchardCore.Products.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Products.Endpoints;

/// <summary>
/// The product search behind the merchant's product pickers, in the shape the item selector reads.
/// </summary>
internal static class ProductSearchEndpoints
{
    public const string SearchPath = "Admin/api/crestapps/products/search";

    public static IEndpointRouteBuilder AddProductSearchEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(SearchPath, SearchAsync).RequireAuthorization();

        return builder;
    }

    private static async Task<IResult> SearchAsync(string query, HttpContext httpContext, IAuthorizationService authorizationService, ProductRecommendationService recommendations)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.EditContent))
        {
            return Results.Forbid();
        }

        var products = await recommendations.SearchAsync(query);

        return Results.Ok(products.Select(product => new { value = product.Id, text = product.Text, secondaryText = product.Type }));
    }
}
