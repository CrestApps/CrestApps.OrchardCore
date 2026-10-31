using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IWorkerManager"/>.
/// </summary>
internal sealed class WorkerManager : CatalogManager<WorkerProfile>, IWorkerManager
{
    private readonly WorkerStore _store;
    private readonly TeamStore _teamStore;

    public WorkerManager(
        WorkerStore store,
        TeamStore teamStore,
        IEnumerable<ICatalogEntryHandler<WorkerProfile>> handlers,
        ILogger<CatalogManager<WorkerProfile>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
        _teamStore = teamStore;
    }

    public async Task<WorkerProfile> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        return await LoadedAsync(await _store.FindByUserIdAsync(userId, cancellationToken), cancellationToken);
    }

    public async Task<WorkerProfile> FindByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber))
        {
            return null;
        }

        return await LoadedAsync(await _store.FindByEmployeeNumberAsync(employeeNumber, cancellationToken), cancellationToken);
    }

    public async Task<(int Count, IReadOnlyList<WorkerProfile> Items)> SearchAsync(WorkerQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<string> teamIds = query?.TeamIds;

        if (!string.IsNullOrEmpty(query?.TeamId))
        {
            var subtree = await TeamTree.GetSubtreeIdsAsync(await _teamStore.ListAsync(cancellationToken), query.TeamId);

            teamIds = teamIds is null
                ? subtree.ToArray()
                : subtree.Where(teamIds.Contains).ToArray();
        }

        var page = await _store.SearchAsync(query, teamIds, skip, take, cancellationToken);

        foreach (var worker in page.Items)
        {
            await LoadAsync(worker, cancellationToken);
        }

        return page;
    }

    public async Task<IReadOnlyList<WorkerProfile>> ListByTeamAsync(string teamId, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(teamId);

        var workers = await _store.ListByTeamAsync(teamId, WorkforceDates.ToKey(date), cancellationToken);

        foreach (var worker in workers)
        {
            await LoadAsync(worker, cancellationToken);
        }

        return workers;
    }

    private async Task<WorkerProfile> LoadedAsync(WorkerProfile worker, CancellationToken cancellationToken)
    {
        if (worker is not null)
        {
            await LoadAsync(worker, cancellationToken);
        }

        return worker;
    }
}
