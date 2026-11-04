using CrestApps.OrchardCore.Workforce.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Adds a link to a worker's time off to the rows of the worker list.
/// </summary>
internal sealed class WorkerTimeOffRowDisplayDriver : DisplayDriver<WorkerProfile>
{
    public override IDisplayResult Display(WorkerProfile item, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        return View("WorkerProfile_TimeOff_SummaryAdmin", item).Location("ActionsMenu:5");
    }
}
