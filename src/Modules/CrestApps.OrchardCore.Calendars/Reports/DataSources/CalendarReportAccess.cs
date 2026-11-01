using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Calendars.Reports.DataSources;

/// <summary>
/// Works out which calendars a report's principal may read, through the same rules as the calendar view: the global
/// calendar permissions, ownership, sharing and visibility. A calendar the principal can only see as busy time is not
/// readable. The answer is kept for the principal for the rest of the scope, since a run reads several pages.
/// </summary>
internal sealed class CalendarReportAccess
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICalendarAccessService _accessService;
    private readonly ICalendarService _calendarService;

    private ClaimsPrincipal _user;
    private CalendarReportScope _scope;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarReportAccess"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="accessService">The calendar access service.</param>
    /// <param name="calendarService">The calendar service.</param>
    public CalendarReportAccess(
        IAuthorizationService authorizationService,
        ICalendarAccessService accessService,
        ICalendarService calendarService)
    {
        _authorizationService = authorizationService;
        _accessService = accessService;
        _calendarService = calendarService;
    }

    /// <summary>
    /// Gets the calendars a principal may read.
    /// </summary>
    /// <param name="user">The principal, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The calendars, or <see langword="null"/> when the principal may read none.</returns>
    public async Task<CalendarReportScope> GetScopeAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return null;
        }

        if (ReferenceEquals(user, _user))
        {
            return _scope;
        }

        var everyCalendar = await _authorizationService.AuthorizeAsync(user, CalendarPermissions.ViewAllCalendars);
        var calendars = await _accessService.ListAccessibleAsync(user, CalendarAccessLevel.Read, cancellationToken: cancellationToken);

        _scope = everyCalendar || calendars.Count > 0
            ? new CalendarReportScope(everyCalendar, calendars)
            : null;
        _user = user;

        return _scope;
    }

    /// <summary>
    /// Gets what a principal may do with a calendar that is not in its scope yet, such as one past the number of
    /// calendars listed at once, and remembers it.
    /// </summary>
    /// <param name="user">The principal.</param>
    /// <param name="scope">The principal's scope.</param>
    /// <param name="calendarId">The calendar's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The calendar with the principal's level, or <see langword="null"/> when it does not exist.</returns>
    public async Task<CalendarAccess> FindAsync(ClaimsPrincipal user, CalendarReportScope scope, string calendarId, CancellationToken cancellationToken)
    {
        if (scope.TryGet(calendarId, out var known))
        {
            return known;
        }

        if (string.IsNullOrEmpty(calendarId))
        {
            return null;
        }

        var calendar = await _calendarService.FindByIdAsync(calendarId, cancellationToken);

        if (calendar is null)
        {
            return null;
        }

        var access = new CalendarAccess(calendar, await _accessService.GetAccessLevelAsync(user, calendar, cancellationToken));

        scope.Add(access);

        return access;
    }
}
