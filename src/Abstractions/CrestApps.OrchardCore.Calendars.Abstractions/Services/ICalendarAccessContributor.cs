using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Grants users access to calendars for reasons the calendar itself doesn't record, such as belonging to the team a
/// module keeps the calendar for. A contributor can only add access: the user's level is the highest of the calendar's
/// own rules and every contributor's answer. It is asked only for users who may use calendars at all.
/// </summary>
public interface ICalendarAccessContributor
{
    /// <summary>
    /// Gets the access a user has to a calendar through this contributor.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="calendar">The calendar.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The level, or <see cref="CalendarAccessLevel.None"/> when the contributor grants nothing.</returns>
    Task<CalendarAccessLevel> GetAccessLevelAsync(ClaimsPrincipal user, Calendar calendar, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the calendars this contributor grants a user some access to, so they appear among the user's calendars.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The calendar ids.</returns>
    Task<IReadOnlyCollection<string>> GetCalendarIdsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
