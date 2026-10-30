using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The rows of the categories list.
/// </summary>
internal sealed class CalendarCategoryDisplayDriver : DisplayDriver<CalendarCategory>
{
    public override IDisplayResult Display(CalendarCategory category, BuildDisplayContext context)
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
            View("CalendarCategory_Fields_SummaryAdmin", category)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("CalendarCategory_Tags_SummaryAdmin", category)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("CalendarCategory_Buttons_SummaryAdmin", category).Location("Actions:5"),
            View("CalendarCategory_ActionsMenu_SummaryAdmin", category).Location("ActionsMenu:10"));
    }
}
