using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Inventory.Drivers;

/// <summary>
/// Renders a stock item as a row of the inventory admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class StockItemDisplayDriver : DisplayDriver<StockItem>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StockItemDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(StockItem item, BuildDisplayContext context)
    {
        // A stock item is only displayed as a row of the inventory list; its own page renders its own view.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{item.Title} {item.Sku}".Trim().ToLowerInvariant();
        }

        var canManage = await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InventoryPermissions.ManageInventory);

        return Combine(
            View("StockItem_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("StockItem_Status_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("StockItem_Sku_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Sku:1"),
            View("StockItem_Stock_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:2")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Stock:1"),
            Initialize<StockItemButtonsViewModel>("StockItem_Buttons_SummaryAdmin", model =>
            {
                model.Item = item;
                model.CanManage = canManage;
            }).Location("Actions:5"));
    }
}
