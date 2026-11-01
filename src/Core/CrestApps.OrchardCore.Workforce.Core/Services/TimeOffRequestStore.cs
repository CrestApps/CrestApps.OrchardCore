using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores time-off requests.
/// </summary>
internal sealed class TimeOffRequestStore : DocumentCatalog<TimeOffRequest, TimeOffRequestIndex>
{
    public TimeOffRequestStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <summary>
    /// Lists a worker's open and approved requests that overlap a range.
    /// </summary>
    public async Task<IReadOnlyList<TimeOffRequest>> ListLiveForWorkerAsync(string workerId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var fromKey = WorkforceDates.ToKey(from);
        var toKey = WorkforceDates.ToKey(to);
        var requests = await Session.Query<TimeOffRequest, TimeOffRequestIndex>(index => index.WorkerId == workerId
                && (index.Status == TimeOffRequestStatus.Pending || index.Status == TimeOffRequestStatus.Waitlisted || index.Status == TimeOffRequestStatus.Approved)
                && index.StartDate <= toKey
                && index.EndDate >= fromKey, collection: CollectionName)
            .ListAsync(cancellationToken);

        return requests.ToArray();
    }

    /// <summary>
    /// Lists a worker's open requests under a policy.
    /// </summary>
    public async Task<IReadOnlyList<TimeOffRequest>> ListOpenForWorkerAsync(string workerId, string policyId, CancellationToken cancellationToken = default)
    {
        var requests = await Session.Query<TimeOffRequest, TimeOffRequestIndex>(index => index.WorkerId == workerId
                && index.PolicyId == policyId
                && (index.Status == TimeOffRequestStatus.Pending || index.Status == TimeOffRequestStatus.Waitlisted), collection: CollectionName)
            .ListAsync(cancellationToken);

        return requests.ToArray();
    }

    /// <summary>
    /// Lists the requests of some teams in some statuses that overlap a range, in the order they were submitted.
    /// </summary>
    public async Task<IReadOnlyList<TimeOffRequest>> ListForTeamsAsync(IReadOnlyCollection<string> teamIds, IReadOnlyCollection<TimeOffRequestStatus> statuses, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (teamIds.Count == 0 || statuses.Count == 0)
        {
            return [];
        }

        var fromKey = WorkforceDates.ToKey(from);
        var toKey = WorkforceDates.ToKey(to);
        var requests = await Session.Query<TimeOffRequest, TimeOffRequestIndex>(index => index.TeamId.IsIn(teamIds)
                && index.Status.IsIn(statuses)
                && index.StartDate <= toKey
                && index.EndDate >= fromKey, collection: CollectionName)
            .OrderBy(index => index.SubmittedUtc)
            .ListAsync(cancellationToken);

        return requests.ToArray();
    }

    public async Task<(int Count, IReadOnlyList<TimeOffRequest> Items)> PageAsync(TimeOffRequestQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var search = Session.Query<TimeOffRequest, TimeOffRequestIndex>(collection: CollectionName);

        if (query.TeamIds is not null)
        {
            if (query.TeamIds.Count == 0)
            {
                return (0, []);
            }

            search = search.Where(index => index.TeamId.IsIn(query.TeamIds));
        }

        if (!string.IsNullOrEmpty(query.WorkerId))
        {
            var workerId = query.WorkerId;

            search = search.Where(index => index.WorkerId == workerId);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;

            search = search.Where(index => index.Status == status);
        }
        else
        {
            search = search.Where(index => index.Status == TimeOffRequestStatus.Pending || index.Status == TimeOffRequestStatus.Waitlisted);
        }

        if (query.From.HasValue)
        {
            var from = WorkforceDates.ToKey(query.From.Value);

            search = search.Where(index => index.EndDate >= from);
        }

        if (query.To.HasValue)
        {
            var to = WorkforceDates.ToKey(query.To.Value);

            search = search.Where(index => index.StartDate <= to);
        }

        var count = await search.CountAsync(cancellationToken);
        var page = await search.OrderBy(index => index.StartDate).ThenBy(index => index.SubmittedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
