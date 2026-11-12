using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Adds the time clock and the timesheet to a worker's actions in the worker list.
/// </summary>
internal sealed class WorkerTimeClockRowDisplayDriver : DisplayDriver<StaffMember>
{
    public override IDisplayResult Display(StaffMember item, BuildDisplayContext context)
    {
        if (context.DisplayType == StaffProfileSections.DisplayType)
        {
            return View("WorkerProfile_TimeClock_DetailAdmin", item).Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(StaffProfileSections.Work, "5"));
        }

        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        return View("WorkerProfile_TimeClock_SummaryAdmin", item).Location("ActionsMenu:6");
    }
}
