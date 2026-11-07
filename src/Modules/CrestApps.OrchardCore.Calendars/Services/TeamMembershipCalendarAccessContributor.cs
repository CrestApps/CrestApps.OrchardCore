using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Teams.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Lets the members of a team write to its team calendar, as well as the users in the calendar's role. A team calendar
/// belongs to a role; a team whose linked role is that role gives the calendar to its members (and the members of its
/// sub-teams) through the shared <see cref="ITeamMembershipService"/>, so a team's people reach its calendar without
/// being put in the role. Calendars of existing sites keep working by role alone.
/// </summary>
internal sealed class TeamMembershipCalendarAccessContributor : ICalendarAccessContributor
{
    private const int MaxCalendars = 500;

    private readonly ITeamManager _teamManager;
    private readonly ITeamMembershipService _membershipService;
    private readonly ICalendarStore _calendarStore;
    private readonly IClock _clock;

    public TeamMembershipCalendarAccessContributor(
        ITeamManager teamManager,
        ITeamMembershipService membershipService,
        ICalendarStore calendarStore,
        IClock clock)
    {
        _teamManager = teamManager;
        _membershipService = membershipService;
        _calendarStore = calendarStore;
        _clock = clock;
    }

    public async Task<CalendarAccessLevel> GetAccessLevelAsync(ClaimsPrincipal user, Calendar calendar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || !IsTeamCalendar(calendar))
        {
            return CalendarAccessLevel.None;
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var teams = await _teamManager.GetAllAsync(cancellationToken);

        foreach (var team in teams.Where(team => team.Enabled && string.Equals(team.LinkedRoleName, calendar.OwnerId, StringComparison.OrdinalIgnoreCase)))
        {
            if (await _membershipService.IsMemberAsync(team.ItemId, userId, today, cancellationToken))
            {
                // The level the calendar gives the users in its role.
                return CalendarAccessLevel.Write;
            }
        }

        return CalendarAccessLevel.None;
    }

    public async Task<IReadOnlyCollection<string>> GetCalendarIdsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var memberships = await _membershipService.GetMembershipsAsync(userId, DateOnly.FromDateTime(_clock.UtcNow), cancellationToken);

        if (memberships.Count == 0)
        {
            return [];
        }

        // A member of a team is a member of every team above it.
        var teamIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var membership in memberships)
        {
            teamIds.Add(membership.TeamId);

            foreach (var ancestor in await _teamManager.GetAncestorsAsync(membership.TeamId, cancellationToken))
            {
                teamIds.Add(ancestor.ItemId);
            }
        }

        var roles = (await _teamManager.GetAllAsync(cancellationToken))
            .Where(team => team.Enabled && teamIds.Contains(team.ItemId) && !string.IsNullOrEmpty(team.LinkedRoleName))
            .Select(team => team.LinkedRoleName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roles.Count == 0)
        {
            return [];
        }

        var query = new CalendarListQuery { Limit = MaxCalendars };
        query.Types.Add(CalendarsConstants.Types.Team);

        foreach (var role in roles)
        {
            query.OwnerIds.Add(role);
        }

        var calendars = await _calendarStore.ListAsync(query, cancellationToken);

        return [.. calendars.Where(IsTeamCalendar).Select(calendar => calendar.ItemId)];
    }

    private static bool IsTeamCalendar(Calendar calendar)
        => calendar.Type == CalendarsConstants.Types.Team
            && calendar.OwnerKind == CalendarOwnerKind.Role
            && !string.IsNullOrEmpty(calendar.OwnerId);
}
