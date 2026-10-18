using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// The Compare toggle on a product's card and page.
/// </summary>
public sealed class ProductCompareActionDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    public override IDisplayResult Display(ProductPart part, BuildPartDisplayContext context)
        => context.DisplayType != ProductListingService.ProductActionDisplayType
            ? null
            : View("ProductCompareToggle", part).Location(ProductListingService.ProductActionDisplayType, "Actions:20");
}
