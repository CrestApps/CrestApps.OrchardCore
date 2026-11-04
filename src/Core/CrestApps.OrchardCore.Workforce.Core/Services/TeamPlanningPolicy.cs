using CrestApps.OrchardCore.Teams.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Models;

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
            if (team.GetWorkforceSettings().OpenShiftClaimPolicy.HasValue)
            {
                return team.GetWorkforceSettings().OpenShiftClaimPolicy.Value;
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
            if (team.GetWorkforceSettings().ShiftTradesNeedApproval.HasValue)
            {
                return team.GetWorkforceSettings().ShiftTradesNeedApproval.Value;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns what happens to a worker's published shifts when their time off is approved; they are flagged by
    /// default.
    /// </summary>
    public async Task<TimeOffShiftHandling> TimeOffShiftHandlingAsync(string teamId, CancellationToken cancellationToken = default)
    {
        foreach (var team in await LineageAsync(teamId, cancellationToken))
        {
            if (team.GetWorkforceSettings().TimeOffShiftHandling.HasValue)
            {
                return team.GetWorkforceSettings().TimeOffShiftHandling.Value;
            }
        }

        return TimeOffShiftHandling.Flag;
    }

    private async Task<IReadOnlyList<Team>> LineageAsync(string teamId, CancellationToken cancellationToken)
    {
        var team = string.IsNullOrEmpty(teamId) ? null : await _teamStore.FindByIdAsync(teamId, cancellationToken);

        if (team is null)
        {
            return [];
        }

        return [team, .. await _teamManager.GetAncestorsAsync(team.ItemId, cancellationToken)];
    }
}
