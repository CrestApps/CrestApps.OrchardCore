using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// Lays out a product's own page: a breadcrumb above the title, the images beside the price and the actions,
/// and the specification table after the description. Each is its own shape, so a theme can move, restyle or
/// replace any of them.
/// </summary>
public sealed class ProductDetailsDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private readonly ProductListingService _listingService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProductDetailsDisplayDriver(
        ProductListingService listingService,
        IHttpContextAccessor httpContextAccessor)
    {
        _listingService = listingService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Display(ProductPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != "Detail")
        {
            return null;
        }

        var pageSettings = context.TypePartDefinition.GetSettings<ProductPageSettings>();

        if (!pageSettings.ShowDetails)
        {
            return null;
        }

        // Built from the version being shown, so a preview shows the draft's values.
        async ValueTask LoadAsync(ProductDetailsShapeViewModel model)
            => model.Product = await _listingService.GetProductAsync(part.ContentItem, pageSettings, _httpContextAccessor.HttpContext);

        return Combine(
            Initialize<ProductDetailsShapeViewModel>("ProductBreadcrumb", LoadAsync).Location("Detail", "Header:before"),
            Initialize<ProductDetailsShapeViewModel>("ProductSummary", LoadAsync).Location("Detail", "Content:before"),
            Initialize<ProductDetailsShapeViewModel>("ProductSpecifications", LoadAsync).Location("Detail", "Content:50"));
    }
}
