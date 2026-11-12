using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Refuses to have a team deleted while it has current members. Its Workforce calendar is kept when it is deleted.
/// </summary>
internal sealed class WorkforceTeamDeletionGuard : ITeamDeletionGuard
{
    private readonly StaffMemberStore _workerStore;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public WorkforceTeamDeletionGuard(
        StaffMemberStore workerStore,
        IClock clock,
        IStringLocalizer<WorkforceTeamDeletionGuard> stringLocalizer)
    {
        _workerStore = workerStore;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<string> GetBlockerAsync(Team team, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        var members = await _workerStore.ListByTeamAsync(team.ItemId, WorkforceDates.ToKey(DateOnly.FromDateTime(_clock.UtcNow)), cancellationToken);

        return members.Count > 0
            ? S["The team still has workers. Move them to another team first."]
            : null;
    }
}
