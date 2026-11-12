using CrestApps.OrchardCore.Staff.Core.Indexes;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Staff.Core.Services;

/// <summary>
/// Stores staff members, one document each, in the <see cref="StaffConstants.CollectionName"/> collection. Its writes
/// skip the catalog handlers; write through <see cref="IStaffMemberManager"/> when they must run.
/// </summary>
public sealed class StaffMemberStore : DocumentCatalog<StaffMember, StaffMemberIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StaffMemberStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public StaffMemberStore(ISession session)
        : base(session)
    {
        CollectionName = StaffConstants.CollectionName;
    }

    /// <summary>
    /// Finds the staff member linked to a user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff member, or <see langword="null"/>.</returns>
    public Task<StaffMember> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult<StaffMember>(null);
        }

        return Session.Query<StaffMember, StaffMemberIndex>(index => index.UserId == userId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Finds a staff member by employee number.
    /// </summary>
    /// <param name="employeeNumber">The employee number.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff member, or <see langword="null"/>.</returns>
    public Task<StaffMember> FindByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default)
    {
        var normalized = StaffKeys.Normalize(employeeNumber);

        if (normalized is null)
        {
            return Task.FromResult<StaffMember>(null);
        }

        return Session.Query<StaffMember, StaffMemberIndex>(index => index.NormalizedEmployeeNumber == normalized, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the staff members linked to users.
    /// </summary>
    /// <param name="userIds">The users.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff members.</returns>
    public async Task<IReadOnlyList<StaffMember>> GetByUserIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = (userIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var items = await Session.Query<StaffMember, StaffMemberIndex>(index => index.UserId.IsIn(ids), collection: CollectionName)
            .ListAsync(cancellationToken);

        return items.ToArray();
    }

    /// <summary>
    /// Searches staff members, ordered by name.
    /// </summary>
    /// <param name="query">The filters.</param>
    /// <param name="teamIds">The teams to match, already expanded, or <see langword="null"/> for any.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The total that match and the page.</returns>
    public async Task<(int Count, IReadOnlyList<StaffMember> Items)> SearchAsync(StaffQuery query, IReadOnlyCollection<string> teamIds, int skip, int take, CancellationToken cancellationToken = default)
    {
        var results = Session.Query<StaffMember, StaffMemberIndex>(collection: CollectionName);

        if (!string.IsNullOrWhiteSpace(query?.Search))
        {
            var search = query.Search.Trim();
            var normalized = search.ToUpperInvariant();

            results = results.Where(index => index.DisplayName.Contains(search)
                || index.Email.Contains(search)
                || index.JobTitle.Contains(search)
                || index.NormalizedEmployeeNumber.Contains(normalized));
        }

        if (query?.Status is { } status)
        {
            results = results.Where(index => index.Status == status);
        }

        if (query?.HasUser is { } hasUser)
        {
            results = hasUser
                ? results.Where(index => index.UserId != null && index.UserId != "")
                : results.Where(index => index.UserId == null || index.UserId == "");
        }

        if (!string.IsNullOrEmpty(query?.ManagerId))
        {
            var managerId = query.ManagerId;

            results = results.Where(index => index.ManagerId == managerId);
        }

        HashSet<string> restrictTo = null;

        if (query?.Ids is not null)
        {
            restrictTo = query.Ids.Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);
        }

        if (teamIds is not null)
        {
            if (teamIds.Count == 0)
            {
                return (0, []);
            }

            var ids = teamIds.ToArray();
            var memberships = await Session.QueryIndex<StaffTeamMembershipIndex>(index => index.TeamId.IsIn(ids), collection: CollectionName).ListAsync(cancellationToken);

            restrictTo = Intersect(restrictTo, memberships.Select(membership => membership.StaffMemberId));
        }

        if (!string.IsNullOrWhiteSpace(query?.Capability))
        {
            var capability = query.Capability.Trim();
            var rows = await Session.QueryIndex<StaffCapabilityIndex>(index => index.Capability == capability, collection: CollectionName).ListAsync(cancellationToken);

            restrictTo = Intersect(restrictTo, rows.Select(row => row.StaffMemberId));
        }

        if (restrictTo is not null)
        {
            if (restrictTo.Count == 0)
            {
                return (0, []);
            }

            var ids = restrictTo.ToArray();

            results = results.Where(index => index.ItemId.IsIn(ids));
        }

        var count = await results.CountAsync(cancellationToken);
        var page = await results.OrderBy(index => index.DisplayName).Skip(Math.Max(0, skip)).Take(Math.Max(1, take)).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }

    /// <summary>
    /// Lists the members of a team at any time from one day key to another.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="fromKey">The first day, as a key.</param>
    /// <param name="toKey">The last day, as a key.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff members, ordered by name.</returns>
    public async Task<IReadOnlyList<StaffMember>> ListByTeamInRangeAsync(string teamId, int fromKey, int toKey, CancellationToken cancellationToken = default)
    {
        var members = await Session.Query<StaffMember, StaffTeamMembershipIndex>(
            index => index.TeamId == teamId
                && (index.EffectiveFrom == null || index.EffectiveFrom <= toKey)
                && (index.EffectiveTo == null || index.EffectiveTo >= fromKey),
            collection: CollectionName)
            .ListAsync(cancellationToken);

        return members
            .DistinctBy(member => member.ItemId)
            .OrderBy(member => member.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Lists the members of a team on a day key.
    /// </summary>
    /// <param name="teamId">The team.</param>
    /// <param name="dateKey">The day, as a key.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff members, ordered by name.</returns>
    public Task<IReadOnlyList<StaffMember>> ListByTeamAsync(string teamId, int dateKey, CancellationToken cancellationToken = default)
        => ListByTeamInRangeAsync(teamId, dateKey, dateKey, cancellationToken);

    /// <summary>
    /// Lists the staff members who report straight to a manager.
    /// </summary>
    /// <param name="managerId">The manager.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The staff members, ordered by name.</returns>
    public async Task<IReadOnlyList<StaffMember>> ListByManagerAsync(string managerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(managerId))
        {
            return [];
        }

        var members = await Session.Query<StaffMember, StaffMemberIndex>(index => index.ManagerId == managerId, collection: CollectionName)
            .OrderBy(index => index.DisplayName)
            .ListAsync(cancellationToken);

        return members.ToArray();
    }

    /// <summary>
    /// Gets the id and manager of every staff member who has a manager, for walking the reporting line.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The manager of each staff member, by staff member id.</returns>
    public async Task<IReadOnlyDictionary<string, string>> GetManagersAsync(CancellationToken cancellationToken = default)
    {
        var rows = await Session.QueryIndex<StaffMemberIndex>(index => index.ManagerId != null, collection: CollectionName).ListAsync(cancellationToken);

        return rows
            .Where(row => !string.IsNullOrEmpty(row.ItemId) && !string.IsNullOrEmpty(row.ManagerId))
            .GroupBy(row => row.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().ManagerId, StringComparer.Ordinal);
    }

    private static HashSet<string> Intersect(HashSet<string> current, IEnumerable<string> ids)
    {
        var found = ids.Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);

        if (current is null)
        {
            return found;
        }

        current.IntersectWith(found);

        return current;
    }
}

/// <summary>
/// Stores the employment history of staff members.
/// </summary>
public sealed class StaffHistoryStore : DocumentCatalog<StaffHistoryEntry, StaffHistoryIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StaffHistoryStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public StaffHistoryStore(ISession session)
        : base(session)
    {
        CollectionName = StaffConstants.CollectionName;
    }

    /// <summary>
    /// Lists a staff member's history, newest first.
    /// </summary>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="take">The most entries to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entries.</returns>
    public async Task<IReadOnlyList<StaffHistoryEntry>> ListAsync(string staffMemberId, int take, CancellationToken cancellationToken = default)
    {
        var items = await Session.Query<StaffHistoryEntry, StaffHistoryIndex>(index => index.StaffMemberId == staffMemberId, collection: CollectionName)
            .OrderByDescending(index => index.ChangedUtc)
            .Take(Math.Max(1, take))
            .ListAsync(cancellationToken);

        return items.ToArray();
    }
}
