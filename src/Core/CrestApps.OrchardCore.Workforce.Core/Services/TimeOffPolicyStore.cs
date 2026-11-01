using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores time-off policies.
/// </summary>
internal sealed class TimeOffPolicyStore : DocumentCatalog<TimeOffPolicy, TimeOffPolicyIndex>
{
    public TimeOffPolicyStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public Task<IReadOnlyList<TimeOffPolicy>> ListAsync(CancellationToken cancellationToken = default)
    {
        return ListByNameAsync(index => index.Name, cancellationToken);
    }

    /// <summary>
    /// Lists the policies assigned to a worker or to any of some teams.
    /// </summary>
    public async Task<IReadOnlyList<TimeOffPolicy>> ListAssignedAsync(string workerId, IReadOnlyCollection<string> teamIds, CancellationToken cancellationToken = default)
    {
        var teams = teamIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        var policies = teams.Length == 0
            ? await Session.Query<TimeOffPolicy, TimeOffPolicyAssignmentIndex>(index => index.WorkerId == workerId, collection: CollectionName).ListAsync(cancellationToken)
            : await Session.Query<TimeOffPolicy, TimeOffPolicyAssignmentIndex>(index => index.WorkerId == workerId || index.TeamId.IsIn(teams), collection: CollectionName).ListAsync(cancellationToken);

        return policies.DistinctBy(policy => policy.ItemId).ToArray();
    }

    /// <summary>
    /// Lists the enabled policies that accrue.
    /// </summary>
    public async Task<IReadOnlyList<TimeOffPolicy>> ListAccruingAsync(CancellationToken cancellationToken = default)
    {
        var policies = await Session.Query<TimeOffPolicy, TimeOffPolicyIndex>(index => index.Enabled && index.AccrualMethod != TimeOffAccrualMethod.None, collection: CollectionName)
            .ListAsync(cancellationToken);

        return policies.ToArray();
    }
}
