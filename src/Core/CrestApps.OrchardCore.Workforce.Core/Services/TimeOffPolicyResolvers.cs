using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Builds <see cref="TimeOffPolicyResolver"/>s, loading the team tree and the policies assigned to a worker's teams.
/// </summary>
internal sealed class TimeOffPolicyResolvers
{
    private readonly TimeOffPolicyStore _policyStore;
    private readonly TeamStore _teamStore;

    public TimeOffPolicyResolvers(TimeOffPolicyStore policyStore, TeamStore teamStore)
    {
        _policyStore = policyStore;
        _teamStore = teamStore;
    }

    public async Task<TimeOffPolicyResolver> ForWorkerAsync(WorkerProfile worker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var teams = (await _teamStore.ListAsync(cancellationToken)).ToDictionary(team => team.ItemId, StringComparer.Ordinal);
        var lineage = new HashSet<string>(StringComparer.Ordinal);

        foreach (var membership in worker.TeamMemberships)
        {
            var teamId = membership.TeamId;

            while (!string.IsNullOrEmpty(teamId) && lineage.Add(teamId))
            {
                teamId = teams.TryGetValue(teamId, out var team) ? team.ParentId : null;
            }
        }

        var policies = await _policyStore.ListAssignedAsync(worker.ItemId, lineage, cancellationToken);

        return new TimeOffPolicyResolver(worker, policies, teams);
    }
}
