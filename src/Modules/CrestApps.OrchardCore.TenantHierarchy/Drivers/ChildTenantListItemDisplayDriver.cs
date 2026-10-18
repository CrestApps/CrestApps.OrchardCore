using CrestApps.OrchardCore.TenantHierarchy.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.TenantHierarchy.Drivers;

/// <summary>
/// Displays a child tenant as a row of the child tenants list, in the List and Grid layouts of the admin list.
/// </summary>
public sealed class ChildTenantListItemDisplayDriver : DisplayDriver<ChildTenantListItem>
{
    public override Task<IDisplayResult> DisplayAsync(ChildTenantListItem item, BuildDisplayContext context)
    {
        var entry = item.Info.Entry;

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = entry.DisplayName?.ToLowerInvariant() ?? string.Empty;
            rowShape.Attributes["data-entry-id"] = entry.EntryId;

            // The list polls the rows that are being set up or removed, and reloads once they are done.
            if (!string.IsNullOrEmpty(item.StatusUrl))
            {
                rowShape.Attributes["data-status-url"] = item.StatusUrl;
            }
        }

        var hasButtons = item.CanEnter
            || (entry.Status == ChildTenantStatus.Failed && item.CanCreate)
            || (entry.Status == ChildTenantStatus.PendingRemoval && item.CanRemove)
            || (item.Info.State == ChildTenantRuntimeState.ChangedByPlatform && item.CanManage);

        // The settings menu is offered once the child tenant is set up and still owned by this parent.
        var hasMenu = !item.IsBusy
            && item.Info.State != ChildTenantRuntimeState.ChangedByPlatform
            && entry.Status != ChildTenantStatus.Failed;

        return CombineAsync(
            View("ChildTenantListItem_Checkbox_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1")
                .RenderWhen(() => Task.FromResult(item.CanSelect)),
            View("ChildTenantListItem_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ChildTenantListItem_Description_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(entry.Description)
                    || (entry.Status == ChildTenantStatus.Failed && !string.IsNullOrEmpty(entry.Error)))),
            View("ChildTenantListItem_Status_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "State:5"),
            View("ChildTenantListItem_Address_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Address:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(entry.Host))),
            View("ChildTenantListItem_Buttons_SummaryAdmin", item)
                .Location("Actions:5")
                .RenderWhen(() => Task.FromResult(hasButtons)),
            View("ChildTenantListItem_ActionsMenu_SummaryAdmin", item)
                .Location("ActionsMenu:10")
                .RenderWhen(() => Task.FromResult(hasMenu))
        );
    }
}
