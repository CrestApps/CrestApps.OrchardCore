using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Gives Teams the members Workforce keeps: each worker with a user account, from its team memberships
/// (<c>WorkerProfile.TeamMemberships</c>). Read from the membership and worker indexes, so no worker is loaded.
/// </summary>
internal sealed class WorkerTeamMemberProvider : ITeamMemberProvider
{
    /// <summary>
    /// The source name the memberships carry.
    /// </summary>
    public const string SourceName = "Workforce";

    private readonly ISession _session;

    public WorkerTeamMemberProvider(ISession session)
    {
        _session = session;
    }

    public async Task<IReadOnlyList<TeamMember>> GetMembersAsync(IReadOnlyCollection<string> teamIds, CancellationToken cancellationToken = default)
    {
        if (teamIds is null || teamIds.Count == 0)
        {
            return [];
        }

        var ids = teamIds.ToArray();
        var memberships = await _session.QueryIndex<WorkerTeamMembershipIndex>(index => index.TeamId.IsIn(ids), collection: WorkforceConstants.CollectionName)
            .ListAsync(cancellationToken);

        return await ToMembersAsync(memberships, cancellationToken);
    }

    public async Task<IReadOnlyList<TeamMember>> GetMembershipsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var workers = await _session.QueryIndex<WorkerIndex>(index => index.UserId == userId, collection: WorkforceConstants.CollectionName)
            .ListAsync(cancellationToken);
        var workerIds = workers.Select(worker => worker.ItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (workerIds.Length == 0)
        {
            return [];
        }

        var memberships = await _session.QueryIndex<WorkerTeamMembershipIndex>(index => index.WorkerId.IsIn(workerIds), collection: WorkforceConstants.CollectionName)
            .ListAsync(cancellationToken);

        return await ToMembersAsync(memberships, cancellationToken);
    }

    private async Task<IReadOnlyList<TeamMember>> ToMembersAsync(IEnumerable<WorkerTeamMembershipIndex> memberships, CancellationToken cancellationToken)
    {
        var rows = memberships.Where(membership => !string.IsNullOrEmpty(membership.WorkerId) && !string.IsNullOrEmpty(membership.TeamId)).ToArray();

        if (rows.Length == 0)
        {
            return [];
        }

        var workerIds = rows.Select(row => row.WorkerId).Distinct(StringComparer.Ordinal).ToArray();
        var workers = await _session.QueryIndex<WorkerIndex>(index => index.ItemId.IsIn(workerIds), collection: WorkforceConstants.CollectionName)
            .ListAsync(cancellationToken);
        var userIds = workers
            .Where(worker => !string.IsNullOrEmpty(worker.UserId))
            .GroupBy(worker => worker.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().UserId, StringComparer.Ordinal);

        return [.. rows
            .Where(row => userIds.ContainsKey(row.WorkerId))
            .Select(row => new TeamMember
            {
                TeamId = row.TeamId,
                UserId = userIds[row.WorkerId],
                Role = row.Role.ToString(),
                EffectiveFrom = WorkforceDates.FromKey(row.EffectiveFrom),
                EffectiveTo = WorkforceDates.FromKey(row.EffectiveTo),
                Source = SourceName,
            })];
    }
}
