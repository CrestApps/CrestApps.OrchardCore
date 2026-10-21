using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// The link to where an external product is sold, in place of Add to cart on its card and page.
/// </summary>
public sealed class ExternalProductActionDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    public override IDisplayResult Display(ProductPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != ProductListingService.ProductActionDisplayType || string.IsNullOrWhiteSpace(part.ExternalUrl))
        {
            return null;
        }

        return View("ExternalProductAction", part).Location(ProductListingService.ProductActionDisplayType, "Actions:5");
    }
}
