using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// The rows of the team list.
/// </summary>
internal sealed class TeamRowDisplayDriver : DisplayDriver<WorkforceTeam>
{
    private readonly ITeamManager _teamManager;

    public TeamRowDisplayDriver(ITeamManager teamManager)
    {
        _teamManager = teamManager;
    }

    public override async Task<IDisplayResult> DisplayAsync(WorkforceTeam team, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        var parent = string.IsNullOrEmpty(team.ParentId) ? null : await _teamManager.FindByIdAsync(team.ParentId);

        void Populate(TeamRowViewModel model)
        {
            model.Team = team;
            model.ParentName = parent?.Name;
        }

        return Combine(
            Initialize<TeamRowViewModel>("WorkforceTeam_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<TeamRowViewModel>("WorkforceTeam_Tags_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            Initialize<TeamRowViewModel>("WorkforceTeam_Buttons_SummaryAdmin", Populate).Location("Actions:5"),
            Initialize<TeamRowViewModel>("WorkforceTeam_ActionsMenu_SummaryAdmin", Populate).Location("ActionsMenu:10"));
    }
}
