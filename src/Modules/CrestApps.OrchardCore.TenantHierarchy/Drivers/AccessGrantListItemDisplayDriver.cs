using CrestApps.OrchardCore.TenantHierarchy.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.TenantHierarchy.Drivers;

/// <summary>
/// Displays an access grant as a row of the access rules list, in the List and Grid layouts of the admin list.
/// </summary>
public sealed class AccessGrantListItemDisplayDriver : DisplayDriver<AccessGrantListItem>
{
    public override IDisplayResult Display(AccessGrantListItem item, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = item.Grant.PrincipalName?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("AccessGrantListItem_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AccessGrantListItem_Scope_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Scope:5"),
            View("AccessGrantListItem_Roles_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Roles:5"),
            View("AccessGrantListItem_Buttons_SummaryAdmin", item)
                .Location("Actions:5"));
    }
}
