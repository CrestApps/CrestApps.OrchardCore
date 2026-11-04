using CrestApps.OrchardCore.Workforce.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// The rows of the pay period list.
/// </summary>
internal sealed class PayPeriodCalendarRowDisplayDriver : DisplayDriver<PayPeriodCalendar>
{
    public override IDisplayResult Display(PayPeriodCalendar item, BuildDisplayContext context)
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
            View("PayPeriodCalendar_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("PayPeriodCalendar_Tags_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("PayPeriodCalendar_Buttons_SummaryAdmin", item).Location("Actions:5"),
            View("PayPeriodCalendar_ActionsMenu_SummaryAdmin", item).Location("ActionsMenu:10"));
    }
}
