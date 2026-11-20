using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Products.Endpoints;

/// <summary>
/// The product search behind the merchant's product pickers, in the shape the item selector reads.
/// </summary>
internal static class ProductSearchEndpoints
{
    public const string RouteName = "CrestApps.Products.ProductSearch";

    /// <summary>
    /// The name of the route that gives one product as a document line: its name, SKU, price and tax category. Its path
    /// takes the product's id in place of <c>__id__</c>.
    /// </summary>
    public const string LineRouteName = "CrestApps.Products.ProductLine";

    public static IEndpointRouteBuilder AddProductSearchEndpoints(this IEndpointRouteBuilder builder)
    {
        var adminUrlPrefix = builder.ServiceProvider.GetRequiredService<IOptions<AdminOptions>>().Value.AdminUrlPrefix;

        builder.MapGet($"{adminUrlPrefix}/api/crestapps/products/search", SearchAsync)
            .RequireAuthorization()
            .WithName(RouteName);

        builder.MapGet($"{adminUrlPrefix}/api/crestapps/products/{{contentItemId}}/line", LineAsync)
            .RequireAuthorization()
            .WithName(LineRouteName);

        return builder;
    }

    /// <summary>
    /// A product as a document line, for the line editors of other modules (an invoice's product picker).
    /// </summary>
    internal static ProductLine ToLine(ContentItem product)
    {
        ArgumentNullException.ThrowIfNull(product);

        var part = product.TryGet<ProductPart>(out var productPart) ? productPart : null;
        var taxation = product.TryGet<TaxationPart>(out var taxationPart) ? taxationPart : null;

        return new ProductLine(
            product.ContentItemId,
            product.DisplayText,
            part?.Sku,
            part?.Price,
            part?.Currency,
            taxation is { Taxable: true } && !string.IsNullOrWhiteSpace(taxation.TaxCategoryCode) ? taxation.TaxCategoryCode : null);
    }

    private static async Task<IResult> LineAsync(string contentItemId, HttpContext httpContext, IAuthorizationService authorizationService, IContentManager contentManager)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, CommonPermissions.EditContent))
        {
            return Results.Forbid();
        }

        var product = string.IsNullOrEmpty(contentItemId) ? null : await contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (product is null || !product.TryGet<ProductPart>(out _))
        {
            return Results.NotFound();
        }

        var line = ToLine(product);

        return Results.Ok(new { id = line.Id, name = line.Name, sku = line.Sku, price = line.Price, currency = line.Currency, taxCategoryCode = line.TaxCategoryCode });
    }

    internal sealed record ProductLine(string Id, string Name, string Sku, decimal? Price, string Currency, string TaxCategoryCode);

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
