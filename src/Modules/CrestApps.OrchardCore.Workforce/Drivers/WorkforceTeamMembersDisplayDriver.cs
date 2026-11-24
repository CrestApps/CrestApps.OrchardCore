using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Shows a team's workers on the team editor. Workforce keeps team membership on its workers and gives it to Teams as an
/// <c>ITeamMemberProvider</c>; this card shows the same people, read only, with a link to each worker.
/// </summary>
internal sealed class WorkforceTeamMembersDisplayDriver : DisplayDriver<Team>
{
    private readonly IStaffMemberManager _workerManager;
    private readonly IClock _clock;

    public WorkforceTeamMembersDisplayDriver(
        IStaffMemberManager workerManager,
        IClock clock)
    {
        _workerManager = workerManager;
        _clock = clock;
    }

    public override IDisplayResult Edit(Team team, BuildEditorContext context)
    {
        if (context.IsNew || string.IsNullOrEmpty(team.ItemId))
        {
            return null;
        }

        return Initialize<WorkforceTeamMembersViewModel>("WorkforceTeamMembers_Edit", async model =>
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow);
            var workers = await _workerManager.ListByTeamAsync(team.ItemId, today);

            model.TeamId = team.ItemId;
            model.Members = [.. workers.Select(worker => new WorkforceTeamMemberRow(
                worker.ItemId,
                worker.DisplayName,
                worker.MembershipsOn(today).FirstOrDefault(membership => membership.TeamId == team.ItemId)?.Role.ToString(),
                !string.IsNullOrEmpty(worker.UserId)))];
        }).Location("Content:6");
    }
}
