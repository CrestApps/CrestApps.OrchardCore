using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Endpoints;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// The parts of a bundle: rows on the product's editor, each a product and how many, and "In this bundle" on its
/// page.
/// </summary>
public sealed class ProductBundleDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private const int Rows = 6;

    private readonly ProductRecommendationService _products;
    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public ProductBundleDisplayDriver(
        ProductRecommendationService products,
        IContentManager contentManager,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<ProductBundleDisplayDriver> stringLocalizer,
        LinkGenerator linkGenerator)
    {
        _products = products;
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(ProductPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != "Detail" ||
            !part.ContentItem.TryGet<ProductBundlePart>(out var bundle) ||
            bundle.Items is not { Count: > 0 } items)
        {
            return null;
        }

        return Initialize<ProductBundleContentsViewModel>("ProductBundleContents", async model =>
        {
            var found = (await _contentManager.GetAsync(items.Select(item => item.ContentItemId), VersionOptions.Published))
                .ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);

            model.Items = [.. items.Select(item => new ProductBundleContentViewModel
            {
                Title = found.TryGetValue(item.ContentItemId, out var product) ? product.DisplayText : item.Title,
                ContentItem = product,
                Quantity = Math.Max(1, item.Quantity),
            })];
        }).Location("Detail", "Content:6");
    }

    public override IDisplayResult Edit(ProductPart part, BuildPartEditorContext context)
        => Initialize<ProductBundleEditViewModel>("ProductBundle_Edit", async model =>
        {
            var items = part.ContentItem.TryGet<ProductBundlePart>(out var bundle) ? bundle.Items ?? [] : [];
            var names = (await _products.NameAsync([.. items.Select(item => item.ContentItemId)])).ToDictionary(name => name.Id, name => name.Text, StringComparer.Ordinal);
            model.SearchEndpoint = _linkGenerator.GetPathByName(_httpContextAccessor.HttpContext, ProductSearchEndpoints.RouteName);
            model.Items = [.. items.Select(item => new ProductBundleItemViewModel
            {
                ContentItemId = item.ContentItemId,
                Quantity = item.Quantity,
                InitialJson = System.Text.Json.JsonSerializer.Serialize(new[] { new { value = item.ContentItemId, text = names.GetValueOrDefault(item.ContentItemId, item.Title), selected = true } }),
            })];

            // Empty rows to add more, up to the limit.
            var rows = Math.Min(ProductBundlePart.MaxItems, Math.Max(Rows, model.Items.Count + 2));

            while (model.Items.Count < rows)
            {
                model.Items.Add(new ProductBundleItemViewModel { InitialJson = "[]" });
            }
        }).Location("Content:21");

    public override async Task<IDisplayResult> UpdateAsync(ProductPart part, UpdatePartEditorContext context)
    {
        var model = new ProductBundleEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var self = part.ContentItem.ContentItemId;
        var rows = (model.Items ?? []).Where(item => !string.IsNullOrWhiteSpace(item.ContentItemId)).ToList();

        if (rows.Any(item => item.Quantity is not (> 0 and <= 100)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Items), S["Each product in a bundle needs a quantity from 1 to 100."]);
        }

        if (rows.Any(item => item.ContentItemId == self))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Items), S["A bundle can't hold itself."]);
        }

        // The same product twice is one row with both quantities.
        var picked = rows
            .Where(item => item.ContentItemId != self && item.Quantity is > 0 and <= 100)
            .GroupBy(item => item.ContentItemId.Trim(), StringComparer.Ordinal)
            .Select(group => (Id: group.Key, Quantity: Math.Min(100, group.Sum(item => item.Quantity ?? 0))))
            .Take(ProductBundlePart.MaxItems)
            .ToList();
        var parts = (await _contentManager.GetAsync(picked.Select(item => item.Id), VersionOptions.Latest)).ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);

        // A part sold in variants can't say which one the bundle holds.
        if (parts.Values.FirstOrDefault(item => item.TryGet<ProductVariantsPart>(out var variants) && variants.HasVariants) is { } withVariants)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Items), S["{0} is sold in variants, so it can't be part of a bundle. Choose a product without variants.", withVariants.DisplayText]);
        }

        if (parts.Values.FirstOrDefault(item => item.TryGet<ProductBundlePart>(out var inner) && inner.Items is { Count: > 0 }) is { } nested)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Items), S["{0} is a bundle itself. A bundle is made of single products.", nested.DisplayText]);
        }

        part.ContentItem.Alter<ProductBundlePart>(bundle => bundle.Items =
        [
            .. picked
                .Where(item => parts.ContainsKey(item.Id))
                .Select(item => new ProductBundleItem { ContentItemId = item.Id, Title = parts[item.Id].DisplayText, Quantity = item.Quantity }),
        ]);

        return Edit(part, context);
    }
}
