using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Resolves a team's shift planning settings, from the team or the nearest ancestor that sets them.
/// </summary>
internal sealed class TeamPlanningPolicy
{
    private readonly TeamStore _teamStore;
    private readonly ITeamManager _teamManager;

    public TeamPlanningPolicy(TeamStore teamStore, ITeamManager teamManager)
    {
        _teamStore = teamStore;
        _teamManager = teamManager;
    }

    /// <summary>
    /// Returns how the team's open shifts are given; managers approve by default.
    /// </summary>
    public async Task<OpenShiftClaimPolicy> ClaimPolicyAsync(string teamId, CancellationToken cancellationToken = default)
    {
        foreach (var team in await LineageAsync(teamId, cancellationToken))
        {
            if (team.OpenShiftClaimPolicy.HasValue)
            {
                return team.OpenShiftClaimPolicy.Value;
            }
        }

        return OpenShiftClaimPolicy.ManagerApproves;
    }

    /// <summary>
    /// Returns whether a manager approves the team's shift trades; they do by default.
    /// </summary>
    public async Task<bool> TradesNeedApprovalAsync(string teamId, CancellationToken cancellationToken = default)
    {
        foreach (var team in await LineageAsync(teamId, cancellationToken))
        {
            if (team.ShiftTradesNeedApproval.HasValue)
            {
                return team.ShiftTradesNeedApproval.Value;
            }
        }

        return true;
    }

    private async Task<IReadOnlyList<WorkforceTeam>> LineageAsync(string teamId, CancellationToken cancellationToken)
    {
        var team = string.IsNullOrEmpty(teamId) ? null : await _teamStore.FindByIdAsync(teamId, cancellationToken);

        if (team is null)
        {
            return [];
        }

        return [team, .. await _teamManager.GetAncestorsAsync(team.ItemId, cancellationToken)];
    }
}
