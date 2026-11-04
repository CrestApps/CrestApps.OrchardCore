using CrestApps.OrchardCore.Teams.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Adds the Workforce actions to the rows of the team list: the team's workers, adding a worker and its calendar.
/// </summary>
internal sealed class WorkforceTeamRowDisplayDriver : DisplayDriver<Team>
{
    public override IDisplayResult Display(Team team, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        return View("WorkforceTeam_ActionsMenu_SummaryAdmin", team).Location("ActionsMenu:10");
    }
}
