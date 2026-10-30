using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Works out what a user can do with a calendar, from the global calendar permissions, ownership, sharing and visibility.
/// </summary>
public interface ICalendarAccessService
{
    /// <summary>
    /// Gets what a user can do with a calendar.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The access level.</returns>
    Task<CalendarAccessLevel> GetAccessLevelAsync(ClaimsPrincipal user, Calendar calendar, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the calendars a user can reach at a given level or above.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="minimum">The lowest level to include.</param>
    /// <param name="type">A calendar type to limit the list to, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Each calendar with the user's level.</returns>
    Task<IReadOnlyList<CalendarAccess>> ListAccessibleAsync(ClaimsPrincipal user, CalendarAccessLevel minimum, string type = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a user may create calendars of a type.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="type">The calendar type's technical name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the user may create one.</returns>
    Task<bool> CanCreateAsync(ClaimsPrincipal user, string type, CancellationToken cancellationToken = default);
}
