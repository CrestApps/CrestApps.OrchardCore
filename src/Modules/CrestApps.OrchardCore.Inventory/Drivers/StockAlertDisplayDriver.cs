using System.Security.Claims;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.ViewModels;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Inventory.Drivers;

/// <summary>
/// "Email me when it's back" on a sold-out product's page, under Add to cart: for the product, or for whichever of
/// its variants is sold out as the shopper picks it.
/// </summary>
public sealed class StockAlertDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private const string ProductActionDisplayType = "ProductAction";

    private readonly IStockItemStore _stockItems;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StockAlertDisplayDriver(IStockItemStore stockItems, IHttpContextAccessor httpContextAccessor)
    {
        _stockItems = stockItems;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(ProductPart part, BuildPartDisplayContext context)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var contentItem = part.ContentItem;

        // On the product's own page only: a card has no room for a form.
        if (context.DisplayType != ProductActionDisplayType ||
            request?.RouteValues["contentItemId"] is not string shown ||
            shown != contentItem.ContentItemId)
        {
            return null;
        }

        var soldOut = (await _stockItems.ListByProductAsync(contentItem.ContentItemId))
            .Where(item => !item.Archived && item.Available <= 0 && !item.AllowBackorder)
            .Select(item => item.VariantId ?? string.Empty)
            .ToList();

        if (soldOut.Count == 0)
        {
            return null;
        }

        var hasVariants = contentItem.TryGet<ProductVariantsPart>(out var variants) && variants.HasVariants;
        var user = _httpContextAccessor.HttpContext?.User;

        return Initialize<StockAlertFormViewModel>("StockAlertForm", model =>
        {
            model.ContentItemId = contentItem.ContentItemId;
            model.HasVariants = hasVariants;
            model.SoldOutVariantIds = hasVariants ? [.. soldOut.Where(id => id.Length > 0)] : [];
            model.Email = user?.FindFirstValue(ClaimTypes.Email);
            model.ReturnUrl = request.PathBase + request.Path + request.QueryString;
        }).Location(ProductActionDisplayType, "Actions:6");
    }
}
