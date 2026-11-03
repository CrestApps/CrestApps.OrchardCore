using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Works out what a user can do with a calendar. In order: calendar administrators manage every calendar; a user manages the
/// calendars they own; members of a team role write to the team's calendars; sharing entries grant what they say; people who
/// may view all calendars read them; and a shared or public calendar shows its busy time or details to every user of calendars.
/// </summary>
public sealed class DefaultCalendarAccessService : ICalendarAccessService
{
    private const int MaxCalendars = 1000;

    private readonly IAuthorizationService _authorizationService;
    private readonly ICalendarStore _calendarStore;
    private readonly ICalendarTypeManager _typeManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCalendarAccessService"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="calendarStore">The calendar store.</param>
    /// <param name="typeManager">The calendar type manager.</param>
    public DefaultCalendarAccessService(
        IAuthorizationService authorizationService,
        ICalendarStore calendarStore,
        ICalendarTypeManager typeManager)
    {
        _authorizationService = authorizationService;
        _calendarStore = calendarStore;
        _typeManager = typeManager;
    }

    /// <inheritdoc />
    public async Task<CalendarAccessLevel> GetAccessLevelAsync(ClaimsPrincipal user, Calendar calendar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        if (user?.Identity?.IsAuthenticated != true)
        {
            return CalendarAccessLevel.None;
        }

        var grants = await GetGrantsAsync(user);

        return GetLevel(user, calendar, grants);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarAccess>> ListAccessibleAsync(ClaimsPrincipal user, CalendarAccessLevel minimum, string type = null, CancellationToken cancellationToken = default)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        var grants = await GetGrantsAsync(user);
        var candidates = new Dictionary<string, Calendar>(StringComparer.Ordinal);

        if (grants.ViewAll)
        {
            var query = new CalendarListQuery { Limit = MaxCalendars };

            if (!string.IsNullOrEmpty(type))
            {
                query.Types.Add(type);
            }

            Add(candidates, await _calendarStore.ListAsync(query, cancellationToken));
        }
        else if (grants.Access)
        {
            var userId = GetUserId(user);
            var roles = GetRoles(user);
            var owned = new CalendarListQuery { Limit = MaxCalendars };

            if (!string.IsNullOrEmpty(userId))
            {
                owned.OwnerIds.Add(userId);
            }

            foreach (var role in roles)
            {
                owned.OwnerIds.Add(role);
            }

            if (owned.OwnerIds.Count > 0)
            {
                Add(candidates, await _calendarStore.ListAsync(owned, cancellationToken));
            }

            Add(candidates, await _calendarStore.ListSharedWithAsync(userId, roles, MaxCalendars, cancellationToken));
            Add(candidates, await _calendarStore.ListVisibleToEveryoneAsync(MaxCalendars, cancellationToken));
        }

        var result = new List<CalendarAccess>();

        foreach (var calendar in candidates.Values)
        {
            if (!string.IsNullOrEmpty(type) && !string.Equals(calendar.Type, type, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var level = GetLevel(user, calendar, grants);

            if (level >= minimum && level > CalendarAccessLevel.None)
            {
                result.Add(new CalendarAccess(calendar, level));
            }
        }

        return result
            .OrderBy(access => access.Calendar.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> CanCreateAsync(ClaimsPrincipal user, string type, CancellationToken cancellationToken = default)
    {
        var descriptor = _typeManager.FindType(type);

        if (descriptor is null || !descriptor.Creatable || user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(user, CalendarPermissions.ManageAllCalendars))
        {
            return true;
        }

        // A personal calendar is the user's own, so anyone who uses calendars may have one.
        if (descriptor.OwnerKind == CalendarOwnerKind.User && descriptor.OnePerOwner)
        {
            return await _authorizationService.AuthorizeAsync(user, CalendarPermissions.AccessCalendars);
        }

        return await _authorizationService.AuthorizeAsync(user, CalendarPermissions.CreateCalendars);
    }

    /// <summary>
    /// Gets the user id from a principal.
    /// </summary>
    /// <param name="user">The principal.</param>
    /// <returns>The user id, or <see langword="null"/>.</returns>
    public static string GetUserId(ClaimsPrincipal user)
    {
        return user?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Gets the role names from a principal.
    /// </summary>
    /// <param name="user">The principal.</param>
    /// <returns>The role names.</returns>
    public static IReadOnlyList<string> GetRoles(ClaimsPrincipal user)
    {
        if (user is null)
        {
            return [];
        }

        return user.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Where(role => !string.IsNullOrEmpty(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<Grants> GetGrantsAsync(ClaimsPrincipal user)
    {
        var manageAll = await _authorizationService.AuthorizeAsync(user, CalendarPermissions.ManageAllCalendars);
        var viewAll = manageAll || await _authorizationService.AuthorizeAsync(user, CalendarPermissions.ViewAllCalendars);
        var access = viewAll || await _authorizationService.AuthorizeAsync(user, CalendarPermissions.AccessCalendars);

        return new Grants(manageAll, viewAll, access);
    }

    private static CalendarAccessLevel GetLevel(ClaimsPrincipal user, Calendar calendar, Grants grants)
    {
        if (grants.ManageAll)
        {
            return CalendarAccessLevel.Manage;
        }

        var level = grants.ViewAll ? CalendarAccessLevel.Read : CalendarAccessLevel.None;

        if (!grants.Access)
        {
            return level;
        }

        var userId = GetUserId(user);
        var roles = GetRoles(user);

        if (calendar.OwnerKind == CalendarOwnerKind.User && !string.IsNullOrEmpty(userId) && string.Equals(calendar.OwnerId, userId, StringComparison.Ordinal))
        {
            return CalendarAccessLevel.Manage;
        }

        if (calendar.OwnerKind == CalendarOwnerKind.Role && !string.IsNullOrEmpty(calendar.OwnerId) && roles.Contains(calendar.OwnerId, StringComparer.OrdinalIgnoreCase))
        {
            level = Max(level, CalendarAccessLevel.Write);
        }

        foreach (var share in calendar.Shares)
        {
            var matches = share.PrincipalKind == CalendarPrincipalKind.User
                ? !string.IsNullOrEmpty(userId) && string.Equals(share.PrincipalId, userId, StringComparison.Ordinal)
                : roles.Contains(share.PrincipalId, StringComparer.OrdinalIgnoreCase);

            if (matches)
            {
                level = Max(level, share.Access);
            }
        }

        level = calendar.Visibility switch
        {
            CalendarVisibility.Public => Max(level, CalendarAccessLevel.Read),
            CalendarVisibility.Shared => Max(level, CalendarAccessLevel.FreeBusy),
            _ => level,
        };

        return level;
    }

    private static CalendarAccessLevel Max(CalendarAccessLevel first, CalendarAccessLevel second)
    {
        return first > second ? first : second;
    }

    private static void Add(Dictionary<string, Calendar> target, IEnumerable<Calendar> calendars)
    {
        foreach (var calendar in calendars)
        {
            target.TryAdd(calendar.ItemId, calendar);
        }
    }

    private readonly record struct Grants(bool ManageAll, bool ViewAll, bool Access);
}
