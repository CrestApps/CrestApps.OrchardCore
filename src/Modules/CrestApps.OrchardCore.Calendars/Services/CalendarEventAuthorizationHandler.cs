using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Grants the calendar event permissions for a resource, so callers only check the broad permission:
/// <see cref="CalendarPermissions.ListCalendarEvents"/> for one's own <see cref="CalendarSchedule"/> with
/// <see cref="CalendarPermissions.ListOwnCalendarEvents"/>, and <see cref="CalendarPermissions.ViewCalendarEvents"/> for a
/// <see cref="CalendarEvent"/> one created, attends or that is on one's own calendar with
/// <see cref="CalendarPermissions.ViewOwnCalendarEvents"/>, or that is on a calendar one can read.
/// </summary>
internal sealed class CalendarEventAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly Lazy<IAuthorizationService> _authorizationService;
    private readonly Lazy<ICalendarService> _calendarService;
    private readonly Lazy<ICalendarAccessService> _accessService;

    // Listing a schedule checks many events in one request, so each calendar and its access level is looked up once.
    private readonly Dictionary<string, Calendar> _calendars = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CalendarAccessLevel> _levels = new(StringComparer.Ordinal);

    public CalendarEventAuthorizationHandler(
        Lazy<IAuthorizationService> authorizationService,
        Lazy<ICalendarService> calendarService,
        Lazy<ICalendarAccessService> accessService)
    {
        _authorizationService = authorizationService;
        _calendarService = calendarService;
        _accessService = accessService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded)
        {
            return;
        }

        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        if (requirement.Permission.Name == CalendarPermissions.ListCalendarEvents.Name)
        {
            if (context.Resource is CalendarSchedule schedule &&
                string.Equals(schedule.UserId, userId, StringComparison.Ordinal) &&
                await _authorizationService.Value.AuthorizeAsync(context.User, CalendarPermissions.ListOwnCalendarEvents))
            {
                context.Succeed(requirement);
            }

            return;
        }

        if (requirement.Permission.Name != CalendarPermissions.ViewCalendarEvents.Name || context.Resource is not CalendarEvent calendarEvent)
        {
            return;
        }

        var calendar = await GetCalendarAsync(calendarEvent.CalendarId);

        if (IsOwn(calendarEvent, calendar, userId) &&
            await _authorizationService.Value.AuthorizeAsync(context.User, CalendarPermissions.ViewOwnCalendarEvents))
        {
            context.Succeed(requirement);

            return;
        }

        if (calendar is null)
        {
            return;
        }

        // The same rule the calendar view follows: a calendar one can read shows its events, except private ones, which need
        // the right to change the calendar.
        var level = await GetLevelAsync(context.User, calendar);

        if (level >= CalendarAccessLevel.Write ||
            level == CalendarAccessLevel.Read && calendarEvent.Visibility != CalendarEventVisibility.Private)
        {
            context.Succeed(requirement);
        }
    }

    private static bool IsOwn(CalendarEvent calendarEvent, Calendar calendar, string userId)
    {
        return string.Equals(calendarEvent.CreatedBy, userId, StringComparison.Ordinal) ||
            calendarEvent.Attendees.Any(attendee => string.Equals(attendee.UserId, userId, StringComparison.Ordinal)) ||
            calendar is { OwnerKind: CalendarOwnerKind.User } && string.Equals(calendar.OwnerId, userId, StringComparison.Ordinal);
    }

    private async Task<Calendar> GetCalendarAsync(string calendarId)
    {
        if (string.IsNullOrEmpty(calendarId))
        {
            return null;
        }

        if (!_calendars.TryGetValue(calendarId, out var calendar))
        {
            calendar = await _calendarService.Value.FindByIdAsync(calendarId);
            _calendars[calendarId] = calendar;
        }

        return calendar;
    }

    private async Task<CalendarAccessLevel> GetLevelAsync(ClaimsPrincipal user, Calendar calendar)
    {
        if (!_levels.TryGetValue(calendar.ItemId, out var level))
        {
            level = await _accessService.Value.GetAccessLevelAsync(user, calendar);
            _levels[calendar.ItemId] = level;
        }

        return level;
    }
}
