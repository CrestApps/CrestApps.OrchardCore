using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Hides a field's own display on a product page when the product details already show it, in the specification
/// table or in the image gallery, so a year, a color or a photo is not shown twice.
/// </summary>
/// <remarks>
/// It only acts on product types that show product details, and only on fields marked
/// "Show in specifications". Every other field, and every other page, keeps its normal placement.
/// </remarks>
public sealed class ProductDetailsPlacementProvider : IShapePlacementProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductDetailsPlacementProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<IPlacementInfoResolver> BuildPlacementInfoResolverAsync(IBuildShapeContext context)
    {
        if (context is not BuildDisplayContext display ||
            display.DisplayType != "Detail" ||
            GetContentItem(context.Shape) is not { } contentItem)
        {
            return null;
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
        var productPart = definition?.Parts.FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductPart));

        if (productPart is null || !productPart.GetSettings<ProductPageSettings>().ShowDetails)
        {
            return null;
        }

        var hidden = new HashSet<string>(StringComparer.Ordinal);

        foreach (var part in definition.Parts)
        {
            foreach (var field in part.PartDefinition.Fields)
            {
                if (field.GetSettings<ProductAttributeSettings>().ShowInSpecifications)
                {
                    hidden.Add($"{part.Name}-{field.Name}");
                }
            }
        }

        // The product's gallery is shown by the product summary.
        if (ProductGalleryReader.Find(definition, contentItem) is { } gallery)
        {
            hidden.Add($"{gallery.PartName}-{gallery.FieldName}");
        }

        return hidden.Count == 0 ? null : new Resolver(hidden);
    }

    // The content display manager puts the item it is displaying on the content shape's properties.
    private static ContentItem GetContentItem(IShape shape)
        => shape?.Properties.TryGetValue("ContentItem", out var value) == true ? value as ContentItem : null;

    private sealed class Resolver : IPlacementInfoResolver
    {
        private readonly HashSet<string> _hidden;

        public Resolver(HashSet<string> hidden)
        {
            _hidden = hidden;
        }

        public PlacementInfo ResolvePlacement(ShapePlacementContext placementContext)
            => placementContext.Differentiator is not null && _hidden.Contains(placementContext.Differentiator)
                ? PlacementInfo.Hidden
                : null;
    }
}
