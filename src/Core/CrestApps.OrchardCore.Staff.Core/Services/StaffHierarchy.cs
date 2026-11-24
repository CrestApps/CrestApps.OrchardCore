using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OrchardCore.Security.Permissions;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Staff.Core.Services;

/// <summary>
/// The default <see cref="IStaffHierarchy"/>. It reads the manager of every staff member once per request from the
/// index, so walking the reporting line costs one query.
/// </summary>
internal sealed class StaffHierarchy : IStaffHierarchy
{
    private readonly StaffMemberStore _store;
    private IReadOnlyDictionary<string, string> _managers;

    public StaffHierarchy(StaffMemberStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<StaffMember>> GetManagerChainAsync(string staffMemberId, CancellationToken cancellationToken = default)
    {
        var managers = await ManagersAsync(cancellationToken);
        var chain = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { staffMemberId ?? string.Empty };

        for (var current = managers.GetValueOrDefault(staffMemberId ?? string.Empty); !string.IsNullOrEmpty(current) && seen.Add(current); current = managers.GetValueOrDefault(current))
        {
            chain.Add(current);
        }

        if (chain.Count == 0)
        {
            return [];
        }

        var people = (await _store.GetAsync(chain, cancellationToken)).ToDictionary(staff => staff.ItemId, StringComparer.Ordinal);

        return chain.Where(people.ContainsKey).Select(id => people[id]).ToArray();
    }

    public async Task<IReadOnlyList<StaffMember>> GetReportsAsync(string staffMemberId, bool includeIndirect = true, CancellationToken cancellationToken = default)
    {
        var ids = await GetReportIdsAsync(staffMemberId, includeIndirect, cancellationToken);

        if (ids.Count == 0)
        {
            return [];
        }

        return (await _store.GetAsync(ids, cancellationToken))
            .OrderBy(staff => staff.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<bool> ManagesAsync(string managerId, string staffMemberId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(managerId) || string.IsNullOrEmpty(staffMemberId) || managerId == staffMemberId)
        {
            return false;
        }

        var managers = await ManagersAsync(cancellationToken);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = managers.GetValueOrDefault(staffMemberId); !string.IsNullOrEmpty(current) && seen.Add(current); current = managers.GetValueOrDefault(current))
        {
            if (current == managerId)
            {
                return true;
            }
        }

        return false;
    }

    internal async Task<IReadOnlySet<string>> GetReportIdsAsync(string staffMemberId, bool includeIndirect, CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(staffMemberId))
        {
            return result;
        }

        var reportsByManager = (await ManagersAsync(cancellationToken))
            .GroupBy(pair => pair.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).ToArray(), StringComparer.Ordinal);
        var queue = new Queue<string>();

        queue.Enqueue(staffMemberId);

        while (queue.Count > 0)
        {
            var manager = queue.Dequeue();

            foreach (var report in reportsByManager.GetValueOrDefault(manager) ?? [])
            {
                if (report != staffMemberId && result.Add(report) && includeIndirect)
                {
                    queue.Enqueue(report);
                }
            }
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, string>> ManagersAsync(CancellationToken cancellationToken)
        => _managers ??= await _store.GetManagersAsync(cancellationToken);
}

/// <summary>
/// The default <see cref="IStaffScopeService"/>: everybody with <see cref="StaffPermissions.ManageStaff"/>; with
/// <see cref="StaffPermissions.ManageOwnStaff"/>, the person's direct and indirect reports and the members of the teams
/// (and their sub-teams) where they are a manager or scheduler.
/// </summary>
internal sealed class StaffScopeService : IStaffScopeService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly StaffMemberStore _store;
    private readonly StaffHierarchy _hierarchy;
    private readonly TeamStore _teamStore;
    private readonly IClock _clock;
    private readonly StaffOptions _options;
    private readonly Dictionary<string, StaffScope> _cache = new(StringComparer.Ordinal);

    public StaffScopeService(
        IAuthorizationService authorizationService,
        StaffMemberStore store,
        StaffHierarchy hierarchy,
        TeamStore teamStore,
        IClock clock,
        IOptions<StaffOptions> options)
    {
        _options = options.Value;
        _authorizationService = authorizationService;
        _store = store;
        _hierarchy = hierarchy;
        _teamStore = teamStore;
        _clock = clock;
    }

    public async Task<StaffScope> GetScopeAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return StaffScope.None;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        if (_cache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        StaffScope scope;

        if (await AnyAsync(user, [StaffPermissions.ManageStaff, .. _options.ManageEveryonePermissions]))
        {
            scope = StaffScope.Everyone;
        }
        else if (!await AnyAsync(user, [StaffPermissions.ManageOwnStaff, .. _options.ManageOwnPermissions]))
        {
            scope = StaffScope.None;
        }
        else
        {
            var me = await _store.FindByUserIdAsync(userId, cancellationToken);

            if (me is null)
            {
                scope = StaffScope.None;
            }
            else
            {
                var today = DateOnly.FromDateTime(_clock.UtcNow);
                var managedTeams = me.MembershipsOn(today)
                    .Where(membership => membership.Role is TeamMemberRole.Manager or TeamMemberRole.Scheduler)
                    .Select(membership => membership.TeamId)
                    .ToArray();
                var teamIds = new HashSet<string>(StringComparer.Ordinal);

                if (_options.UsesTeams && managedTeams.Length > 0)
                {
                    var teams = await _teamStore.ListAsync(cancellationToken);

                    foreach (var teamId in managedTeams)
                    {
                        teamIds.UnionWith(TeamTree.GetSubtreeIds(teams, teamId));
                    }
                }

                var reports = await _hierarchy.GetReportIdsAsync(me.ItemId, includeIndirect: true, cancellationToken);

                scope = new StaffScope(false, teamIds, new HashSet<string>(reports, StringComparer.Ordinal));
            }
        }

        _cache[userId] = scope;

        return scope;
    }

    private async Task<bool> AnyAsync(ClaimsPrincipal user, IEnumerable<Permission> permissions)
    {
        foreach (var permission in permissions)
        {
            if (await _authorizationService.AuthorizeAsync(user, permission))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> CanManageAsync(ClaimsPrincipal user, StaffMember staffMember, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(staffMember);

        var scope = await GetScopeAsync(user, cancellationToken);

        return scope.Covers(staffMember, DateOnly.FromDateTime(_clock.UtcNow));
    }
}

/// <summary>
/// The default <see cref="IStaffTimeZoneResolver"/>: the person's zone, else their main team's or its parents', else the site's.
/// </summary>
internal sealed class StaffTimeZoneResolver : IStaffTimeZoneResolver
{
    private readonly TeamStore _teamStore;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly StaffOptions _options;

    public StaffTimeZoneResolver(
        TeamStore teamStore,
        ISiteService siteService,
        IClock clock,
        IOptions<StaffOptions> options)
    {
        _options = options.Value;
        _teamStore = teamStore;
        _siteService = siteService;
        _clock = clock;
    }

    public async Task<string> GetTimeZoneIdAsync(StaffMember staffMember, CancellationToken cancellationToken = default)
    {
        if (await FindTimeZoneIdAsync(staffMember, cancellationToken) is { } found)
        {
            return found;
        }

        var siteZone = (await _siteService.GetSiteSettingsAsync()).TimeZoneId;

        return string.IsNullOrEmpty(siteZone) ? "UTC" : siteZone;
    }

    public async Task<string> FindTimeZoneIdAsync(StaffMember staffMember, CancellationToken cancellationToken = default)
    {
        if (staffMember is not null && CalendarTimeZones.IsValid(staffMember.TimeZoneId))
        {
            return staffMember.TimeZoneId;
        }

        if (!_options.UsesTeams)
        {
            return null;
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);

        for (var id = staffMember is null ? null : MainTeam(staffMember, DateOnly.FromDateTime(_clock.UtcNow)); !string.IsNullOrEmpty(id) && visited.Add(id);)
        {
            var team = await _teamStore.FindByIdAsync(id, cancellationToken);

            if (team is null)
            {
                break;
            }

            if (CalendarTimeZones.IsValid(team.TimeZoneId))
            {
                return team.TimeZoneId;
            }

            id = team.ParentId;
        }

        return null;
    }

    /// <summary>
    /// Gets the team that sets a person's day: the main team when it counts on the day, else the first team on the day,
    /// else the first team ever.
    /// </summary>
    /// <param name="staffMember">The staff member.</param>
    /// <param name="date">The day.</param>
    /// <returns>The team, or <see langword="null"/>.</returns>
    public static string MainTeam(StaffMember staffMember, DateOnly date)
    {
        var memberships = staffMember.MembershipsOn(date).Select(membership => membership.TeamId).Where(id => !string.IsNullOrEmpty(id)).ToArray();

        if (!string.IsNullOrEmpty(staffMember.PrimaryTeamId) && (memberships.Length == 0 || memberships.Contains(staffMember.PrimaryTeamId)))
        {
            return staffMember.PrimaryTeamId;
        }

        return memberships.FirstOrDefault() ?? staffMember.TeamMemberships.Select(membership => membership.TeamId).FirstOrDefault(id => !string.IsNullOrEmpty(id));
    }
}

/// <summary>
/// The default <see cref="IStaffHistoryService"/>.
/// </summary>
internal sealed class StaffHistoryService : IStaffHistoryService
{
    private readonly StaffHistoryStore _store;

    public StaffHistoryService(StaffHistoryStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<StaffHistoryEntry>> ListAsync(string staffMemberId, int take = 100, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(staffMemberId))
        {
            return Task.FromResult<IReadOnlyList<StaffHistoryEntry>>([]);
        }

        return _store.ListAsync(staffMemberId, take, cancellationToken);
    }
}
