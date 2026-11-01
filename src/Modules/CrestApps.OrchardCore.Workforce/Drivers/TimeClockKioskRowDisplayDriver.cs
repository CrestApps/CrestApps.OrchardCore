using CrestApps.OrchardCore.Workforce.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// The rows of the kiosk list.
/// </summary>
internal sealed class TimeClockKioskRowDisplayDriver : DisplayDriver<TimeClockKiosk>
{
    public override IDisplayResult Display(TimeClockKiosk item, BuildDisplayContext context)
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
            View("TimeClockKiosk_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("TimeClockKiosk_Tags_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("TimeClockKiosk_Buttons_SummaryAdmin", item).Location("Actions:5"),
            View("TimeClockKiosk_ActionsMenu_SummaryAdmin", item).Location("ActionsMenu:10"));
    }
}
