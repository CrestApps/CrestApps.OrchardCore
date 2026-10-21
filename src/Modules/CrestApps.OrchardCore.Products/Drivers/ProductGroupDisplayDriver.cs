using System.Text.Json;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Endpoints;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// The picker on a product's editor for the products it groups. The Carts module shows them on its page, each with
/// a quantity to buy.
/// </summary>
public sealed class ProductGroupDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private readonly ProductRecommendationService _products;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProductGroupDisplayDriver(
        ProductRecommendationService products,
        IHttpContextAccessor httpContextAccessor)
    {
        _products = products;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Edit(ProductPart part, BuildPartEditorContext context)
        => Initialize<ProductGroupEditViewModel>("ProductGroup_Edit", async model =>
        {
            var pathBase = _httpContextAccessor.HttpContext?.Request.PathBase.Value ?? string.Empty;

            model.ProductIds = [.. part.ContentItem.TryGet<ProductGroupPart>(out var group) ? group.ProductIds ?? [] : []];
            model.SearchEndpoint = $"{pathBase}/{ProductSearchEndpoints.SearchPath}";
            model.InitialProductsJson = JsonSerializer.Serialize((await _products.NameAsync(model.ProductIds)).Select(item => new { value = item.Id, text = item.Text, selected = true }));
        }).Location("Content:19");

    public override async Task<IDisplayResult> UpdateAsync(ProductPart part, UpdatePartEditorContext context)
    {
        var model = new ProductGroupEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var self = part.ContentItem.ContentItemId;

        // A group doesn't hold itself, and holds each product once.
        part.ContentItem.Alter<ProductGroupPart>(group => group.ProductIds =
        [
            .. (model.ProductIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id) && id != self)
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(ProductGroupPart.MaxProducts),
        ]);

        return Edit(part, context);
    }
}
