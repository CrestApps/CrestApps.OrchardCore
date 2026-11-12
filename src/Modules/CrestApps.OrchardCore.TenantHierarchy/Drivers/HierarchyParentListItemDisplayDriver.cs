using CrestApps.OrchardCore.TenantHierarchy.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.TenantHierarchy.Drivers;

/// <summary>
/// Displays a parent tenant as a row of the parent tenants list of the platform, in the List and Grid layouts of the
/// admin list.
/// </summary>
public sealed class HierarchyParentListItemDisplayDriver : DisplayDriver<HierarchyParentListItem>
{
    public override IDisplayResult Display(HierarchyParentListItem item, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        var parent = item.Parent;

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{parent.DisplayName} {parent.Settings.Name}".ToLowerInvariant();
        }

        return Combine(
            View("HierarchyParentListItem_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("HierarchyParentListItem_Description_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("HierarchyParentListItem_Policy_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Policy:5"),
            View("HierarchyParentListItem_State_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "State:5"),
            View("HierarchyParentListItem_Children_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Children:5")
                .RenderWhen(() => Task.FromResult(parent.Children.Count > 0)),
            View("HierarchyParentListItem_Buttons_SummaryAdmin", item)
                .Location("Actions:5"));
    }
}
