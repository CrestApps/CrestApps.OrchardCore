using CrestApps.OrchardCore.Workforce.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Adds the time clock and the timesheet to a worker's actions in the worker list.
/// </summary>
internal sealed class WorkerTimeClockRowDisplayDriver : DisplayDriver<WorkerProfile>
{
    public override IDisplayResult Display(WorkerProfile item, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        return View("WorkerProfile_TimeClock_SummaryAdmin", item).Location("ActionsMenu:6");
    }
}
