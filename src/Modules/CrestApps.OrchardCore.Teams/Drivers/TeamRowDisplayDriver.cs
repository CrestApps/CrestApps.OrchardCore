using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Teams.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Teams.Drivers;

/// <summary>
/// The rows of the team list. Modules that build on teams add their own row actions with drivers of their own,
/// between Add a team under it (<c>ActionsMenu:5</c>) and Delete (<c>ActionsMenu:100</c>).
/// </summary>
internal sealed class TeamRowDisplayDriver : DisplayDriver<Team>
{
    private readonly ITeamManager _teamManager;

    public TeamRowDisplayDriver(ITeamManager teamManager)
    {
        _teamManager = teamManager;
    }

    public override async Task<IDisplayResult> DisplayAsync(Team team, BuildDisplayContext context)
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
            Initialize<TeamRowViewModel>("Team_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<TeamRowViewModel>("Team_Tags_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            Initialize<TeamRowViewModel>("Team_Buttons_SummaryAdmin", Populate).Location("Actions:5"),
            Initialize<TeamRowViewModel>("Team_ActionsMenu_SummaryAdmin", Populate).Location("ActionsMenu:5"),
            Initialize<TeamRowViewModel>("Team_Delete_SummaryAdmin", Populate).Location("ActionsMenu:100"));
    }
}
