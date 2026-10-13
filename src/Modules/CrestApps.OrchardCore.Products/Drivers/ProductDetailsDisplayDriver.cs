using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// Shows the price, the specification table and the product actions on a product's own page.
/// </summary>
public sealed class ProductDetailsDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private readonly ProductSearchDocumentBuilder _documentBuilder;
    private readonly ProductAttributeCatalog _attributeCatalog;
    private readonly ProductListingService _listingService;

    public ProductDetailsDisplayDriver(
        ProductSearchDocumentBuilder documentBuilder,
        ProductAttributeCatalog attributeCatalog,
        ProductListingService listingService)
    {
        _documentBuilder = documentBuilder;
        _attributeCatalog = attributeCatalog;
        _listingService = listingService;
    }

    public override IDisplayResult Display(ProductPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != "Detail" ||
            !context.TypePartDefinition.GetSettings<ProductPageSettings>().ShowDetails)
        {
            return null;
        }

        return Initialize<ProductDetailsViewModel>("ProductDetails", async model =>
        {
            var contentItem = part.ContentItem;
            var pageSettings = context.TypePartDefinition.GetSettings<ProductPageSettings>();

            // Built from the version being shown, so a preview shows the draft's values.
            var document = await _documentBuilder.BuildAsync(contentItem);

            if (document is null)
            {
                return;
            }

            var attributes = await _attributeCatalog.GetAttributesAsync([contentItem.ContentType]);

            model.ContentItem = contentItem;
            model.Price = await _listingService.BuildPriceShapeAsync(document, contentItem.ContentType);
            model.IsPurchasable = document.IsPurchasable;
            model.Sku = document.Sku;
            model.Specifications = _listingService.BuildSpecifications(document, attributes, settings => settings.ShowInSpecifications);
            model.InquiryLabel = pageSettings.InquiryLabel;
            model.InquiryUrl = string.IsNullOrWhiteSpace(pageSettings.InquiryUrl)
                ? null
                : pageSettings.InquiryUrl
                    .Replace("{id}", Uri.EscapeDataString(contentItem.ContentItemId), StringComparison.OrdinalIgnoreCase)
                    .Replace("{title}", Uri.EscapeDataString(contentItem.DisplayText ?? string.Empty), StringComparison.OrdinalIgnoreCase);
        }).Location("Detail", "Content:2");
    }
}
