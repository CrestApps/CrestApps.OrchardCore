using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores workers.
/// </summary>
internal sealed class WorkerStore : DocumentCatalog<WorkerProfile, WorkerIndex>
{
    public WorkerStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public Task<WorkerProfile> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Session.Query<WorkerProfile, WorkerIndex>(index => index.UserId == userId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<WorkerProfile> FindByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default)
    {
        var normalized = WorkforceNames.Normalize(employeeNumber);

        return Session.Query<WorkerProfile, WorkerIndex>(index => index.NormalizedEmployeeNumber == normalized, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(int Count, IReadOnlyList<WorkerProfile> Items)> SearchAsync(WorkerQuery query, IReadOnlyCollection<string> teamIds, int skip, int take, CancellationToken cancellationToken = default)
    {
        var results = Session.Query<WorkerProfile, WorkerIndex>(collection: CollectionName);

        if (!string.IsNullOrWhiteSpace(query?.Search))
        {
            var search = query.Search.Trim();
            var normalized = search.ToUpperInvariant();

            results = results.Where(index => index.DisplayName.Contains(search) || index.Email.Contains(search) || index.NormalizedEmployeeNumber.Contains(normalized));
        }

        if (query?.Status is { } status)
        {
            results = results.Where(index => index.Status == status);
        }

        if (teamIds is not null)
        {
            if (teamIds.Count == 0)
            {
                return (0, []);
            }

            var memberships = await Session.QueryIndex<WorkerTeamMembershipIndex>(index => index.TeamId.IsIn(teamIds), collection: CollectionName).ListAsync(cancellationToken);
            var workerIds = memberships.Select(membership => membership.WorkerId).Distinct(StringComparer.Ordinal).ToArray();

            if (workerIds.Length == 0)
            {
                return (0, []);
            }

            results = results.Where(index => index.ItemId.IsIn(workerIds));
        }

        var count = await results.CountAsync(cancellationToken);
        var page = await results.OrderBy(index => index.DisplayName).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }

    public async Task<IReadOnlyList<WorkerProfile>> ListByTeamAsync(string teamId, int dateKey, CancellationToken cancellationToken = default)
    {
        var workers = await Session.Query<WorkerProfile, WorkerTeamMembershipIndex>(
            index => index.TeamId == teamId
                && (index.EffectiveFrom == null || index.EffectiveFrom <= dateKey)
                && (index.EffectiveTo == null || index.EffectiveTo >= dateKey),
            collection: CollectionName)
            .ListAsync(cancellationToken);

        return workers
            .DistinctBy(worker => worker.ItemId)
            .OrderBy(worker => worker.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
