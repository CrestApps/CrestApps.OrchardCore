using CrestApps.OrchardCore.Staff.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Staff.Drivers;

/// <summary>
/// A row of the Staff &gt; Capabilities list.
/// </summary>
internal sealed class StaffCapabilityRowDisplayDriver : DisplayDriver<StaffCapabilityInfo>
{
    public override IDisplayResult Display(StaffCapabilityInfo capability, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        return Combine(
            View("StaffCapabilityInfo_Fields_SummaryAdmin", capability)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("StaffCapabilityInfo_Description_SummaryAdmin", capability)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("StaffCapabilityInfo_Tags_SummaryAdmin", capability)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Kind:5"),
            View("StaffCapabilityInfo_Buttons_SummaryAdmin", capability).Location("Actions:5"),
            View("StaffCapabilityInfo_ActionsMenu_SummaryAdmin", capability).Location("ActionsMenu:10"));
    }
}
