using CrestApps.OrchardCore.TenantHierarchy.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Drivers;

/// <summary>
/// Displays a child tenant as a row of a child tenants list of the platform (the child tenants of one parent, or
/// the orphaned child tenants), in the List and Grid layouts of the admin list.
/// </summary>
public sealed class HierarchyChildListItemDisplayDriver : DisplayDriver<HierarchyChildListItem>
{
    public override IDisplayResult Display(HierarchyChildListItem item, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        var child = item.Child;

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{child.DisplayName} {child.Settings.Name}".ToLowerInvariant();
        }

        var canView = !string.IsNullOrEmpty(child.Address) && child.Settings.State == TenantState.Running;

        return Combine(
            View("HierarchyChildListItem_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("HierarchyChildListItem_Description_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("HierarchyChildListItem_State_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "State:5"),
            View("HierarchyChildListItem_Address_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Address:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(child.Settings.GetPrimaryHost()))),
            View("HierarchyChildListItem_Buttons_SummaryAdmin", item)
                .Location("Actions:5")
                .RenderWhen(() => Task.FromResult(canView)),
            View("HierarchyChildListItem_ActionsMenu_SummaryAdmin", item)
                .Location("ActionsMenu:10")
                .RenderWhen(() => Task.FromResult(item.IsOrphan || item.MoveTargets.Count > 0)));
    }
}
