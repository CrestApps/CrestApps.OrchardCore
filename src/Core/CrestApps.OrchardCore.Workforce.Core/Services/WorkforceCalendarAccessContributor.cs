using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Gives a team's Workforce calendar to its people: the managers and schedulers of the team or of a team above it manage
/// it, and the team's members and the members of its linked role read it.
/// </summary>
internal sealed class WorkforceCalendarAccessContributor : ICalendarAccessContributor
{
    private readonly IWorkforceScopeService _scopeService;
    private readonly WorkerStore _workerStore;
    private readonly TeamStore _teamStore;
    private readonly IClock _clock;

    public WorkforceCalendarAccessContributor(
        IWorkforceScopeService scopeService,
        WorkerStore workerStore,
        TeamStore teamStore,
        IClock clock)
    {
        _scopeService = scopeService;
        _workerStore = workerStore;
        _teamStore = teamStore;
        _clock = clock;
    }

    public async Task<CalendarAccessLevel> GetAccessLevelAsync(ClaimsPrincipal user, Calendar calendar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        if (calendar.Type != WorkforceConstants.CalendarType || calendar.Source != WorkforceConstants.Source || string.IsNullOrEmpty(calendar.SourceId))
        {
            return CalendarAccessLevel.None;
        }

        if ((await _scopeService.GetManagedScopeAsync(user, cancellationToken)).Covers(calendar.SourceId))
        {
            return CalendarAccessLevel.Manage;
        }

        var teamIds = await GetMemberTeamIdsAsync(user, cancellationToken);

        if (teamIds.Contains(calendar.SourceId))
        {
            return CalendarAccessLevel.Read;
        }

        var team = await _teamStore.FindByIdAsync(calendar.SourceId, cancellationToken);

        return team?.LinkedRoleName is { } role && user.IsInRole(role)
            ? CalendarAccessLevel.Read
            : CalendarAccessLevel.None;
    }

    public async Task<IReadOnlyCollection<string>> GetCalendarIdsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var scope = await _scopeService.GetManagedScopeAsync(user, cancellationToken);
        var memberTeams = await GetMemberTeamIdsAsync(user, cancellationToken);
        var teams = await _teamStore.ListAsync(cancellationToken);

        return teams
            .Where(team => !string.IsNullOrEmpty(team.CalendarId)
                && (scope.Covers(team.ItemId) || memberTeams.Contains(team.ItemId) || (team.LinkedRoleName is { } role && user.IsInRole(role))))
            .Select(team => team.CalendarId)
            .ToArray();
    }

    private async Task<HashSet<string>> GetMemberTeamIdsAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var worker = string.IsNullOrEmpty(userId) ? null : await _workerStore.FindByUserIdAsync(userId, cancellationToken);
        var today = DateOnly.FromDateTime(_clock.UtcNow);

        return worker?.MembershipsOn(today).Select(membership => membership.TeamId).ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
    }
}
