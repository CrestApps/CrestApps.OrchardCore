using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Inventory.Drivers;

/// <summary>
/// Renders a purchase order as a row of the purchase orders admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class PurchaseOrderDisplayDriver : DisplayDriver<PurchaseOrder>
{
    private readonly IInventoryLocationService _locations;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PurchaseOrderDisplayDriver(
        IInventoryLocationService locations,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _locations = locations;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(PurchaseOrder order, BuildDisplayContext context)
    {
        // A purchase order is only displayed as a row of the list; its own page renders its own view.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{order.Number} {order.Supplier}".Trim().ToLowerInvariant();
        }

        var locationName = (await _locations.GetLocationsAsync()).FirstOrDefault(location => location.LocationId == order.LocationId)?.Name ?? order.LocationId;
        var canManage = await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InventoryPermissions.ManageInventory);

        void Fill(PurchaseOrderRowViewModel model)
        {
            model.Order = order;
            model.LocationName = locationName;
            model.CanManage = canManage;
        }

        return Combine(
            Initialize<PurchaseOrderRowViewModel>("PurchaseOrder_Fields_SummaryAdmin", Fill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<PurchaseOrderRowViewModel>("PurchaseOrder_Status_SummaryAdmin", Fill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            Initialize<PurchaseOrderRowViewModel>("PurchaseOrder_Meta_SummaryAdmin", Fill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Items:1"),
            Initialize<PurchaseOrderRowViewModel>("PurchaseOrder_Description_SummaryAdmin", Fill)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            Initialize<PurchaseOrderRowViewModel>("PurchaseOrder_Buttons_SummaryAdmin", Fill).Location("Actions:5"));
    }
}
