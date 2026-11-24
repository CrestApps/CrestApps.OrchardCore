using CrestApps.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Staff.Core.Services;

/// <summary>
/// The default <see cref="IStaffMemberManager"/>.
/// </summary>
internal sealed class StaffMemberManager : CatalogManager<StaffMember>, IStaffMemberManager
{
    private readonly StaffMemberStore _store;
    private readonly TeamStore _teamStore;

    public StaffMemberManager(
        StaffMemberStore store,
        TeamStore teamStore,
        IEnumerable<ICatalogEntryHandler<StaffMember>> handlers,
        ILogger<CatalogManager<StaffMember>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
        _teamStore = teamStore;
    }

    public async Task<StaffMember> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        => await LoadedAsync(await _store.FindByUserIdAsync(userId, cancellationToken), cancellationToken);

    public async Task<StaffMember> FindByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default)
        => await LoadedAsync(await _store.FindByEmployeeNumberAsync(employeeNumber, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<StaffMember>> GetByUserIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
        => await LoadedAsync(await _store.GetByUserIdsAsync(userIds, cancellationToken), cancellationToken);

    public async Task<(int Count, IReadOnlyList<StaffMember> Items)> SearchAsync(StaffQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<string> teamIds = query?.TeamIds;

        if (!string.IsNullOrEmpty(query?.TeamId))
        {
            var subtree = TeamTree.GetSubtreeIds(await _teamStore.ListAsync(cancellationToken), query.TeamId);

            teamIds = teamIds is null
                ? subtree.ToArray()
                : subtree.Where(teamIds.Contains).ToArray();
        }

        var page = await _store.SearchAsync(query, teamIds, skip, take, cancellationToken);

        return (page.Count, await LoadedAsync(page.Items, cancellationToken));
    }

    public Task<IReadOnlyList<StaffMember>> ListByTeamAsync(string teamId, DateOnly date, CancellationToken cancellationToken = default)
        => ListByTeamInRangeAsync(teamId, date, date, cancellationToken);

    public async Task<IReadOnlyList<StaffMember>> ListByTeamInRangeAsync(string teamId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(teamId);

        return await LoadedAsync(await _store.ListByTeamInRangeAsync(teamId, StaffKeys.ToKey(from), StaffKeys.ToKey(to), cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<StaffMember>> ListDirectReportsAsync(string managerId, CancellationToken cancellationToken = default)
        => await LoadedAsync(await _store.ListByManagerAsync(managerId, cancellationToken), cancellationToken);

    private async Task<StaffMember> LoadedAsync(StaffMember staffMember, CancellationToken cancellationToken)
    {
        if (staffMember is not null)
        {
            await LoadAsync(staffMember, cancellationToken);
        }

        return staffMember;
    }

    private async Task<IReadOnlyList<StaffMember>> LoadedAsync(IReadOnlyList<StaffMember> items, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            await LoadAsync(item, cancellationToken);
        }

        return items;
    }
}
