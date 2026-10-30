using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// The rules the admin screens, the API, the workflow tasks and the AI tools share when they change events: who may change an
/// event, and which fields an editor may change.
/// </summary>
public sealed class CalendarEventEditor
{
    private readonly ICalendarAccessService _accessService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventEditor"/> class.
    /// </summary>
    /// <param name="accessService">The calendar access service.</param>
    public CalendarEventEditor(ICalendarAccessService accessService)
    {
        _accessService = accessService;
    }

    /// <summary>
    /// Gets whether a user may add events to a calendar.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="calendar">The calendar.</param>
    /// <returns><see langword="true"/> when the user may add events.</returns>
    public async Task<bool> CanAddAsync(ClaimsPrincipal user, Calendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return calendar.Enabled && await _accessService.GetAccessLevelAsync(user, calendar) >= CalendarAccessLevel.Write;
    }

    /// <summary>
    /// Gets whether a user may change an event: they can write to its calendar and the event isn't owned by a module that keeps
    /// it read-only.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="calendar">The event's calendar.</param>
    /// <returns><see langword="true"/> when the user may change it.</returns>
    public async Task<bool> CanChangeAsync(ClaimsPrincipal user, CalendarEvent calendarEvent, Calendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        ArgumentNullException.ThrowIfNull(calendar);

        return calendarEvent.Editable && await _accessService.GetAccessLevelAsync(user, calendar) >= CalendarAccessLevel.Write;
    }

    /// <summary>
    /// Gets whether a user may see an event's details: they can read its calendar, or they are invited.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="calendarEvent">The event.</param>
    /// <param name="calendar">The event's calendar.</param>
    /// <returns><see langword="true"/> when the user may see the details.</returns>
    public async Task<bool> CanReadAsync(ClaimsPrincipal user, CalendarEvent calendarEvent, Calendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        ArgumentNullException.ThrowIfNull(calendar);

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(userId) && calendarEvent.Attendees.Any(attendee => attendee.UserId == userId))
        {
            return true;
        }

        var level = await _accessService.GetAccessLevelAsync(user, calendar);

        return level >= CalendarAccessLevel.Write || (level == CalendarAccessLevel.Read && calendarEvent.Visibility != CalendarEventVisibility.Private);
    }

    /// <summary>
    /// Copies the fields an editor may change from one event onto another.
    /// </summary>
    /// <param name="source">The edited values.</param>
    /// <param name="target">The event to change.</param>
    /// <param name="includeRecurrence">Whether to copy how the event repeats.</param>
    public static void CopyEditableFields(CalendarEvent source, CalendarEvent target, bool includeRecurrence)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        target.Title = source.Title;
        target.Description = source.Description;
        target.Location = source.Location;
        target.EventType = source.EventType;
        target.AllDay = source.AllDay;
        target.Start = source.Start;
        target.End = source.End;
        target.TimeZoneId = source.TimeZoneId;
        target.Status = source.Status;
        target.Transparency = source.Transparency;
        target.Visibility = source.Visibility;
        target.Color = source.Color;
        target.CategoryId = source.CategoryId;
        target.ReminderMinutes = source.ReminderMinutes.ToList();
        target.Attendees = source.Attendees.ToList();
        target.Properties = new Dictionary<string, object>(source.Properties, StringComparer.OrdinalIgnoreCase);

        if (includeRecurrence)
        {
            target.Recurrence = source.Recurrence;
        }
    }

    /// <summary>
    /// Adds calendar errors to a model state, under the editor's prefix.
    /// </summary>
    /// <param name="modelState">The model state.</param>
    /// <param name="errors">The errors.</param>
    public static void AddErrors(ModelStateDictionary modelState, IEnumerable<CalendarError> errors)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        foreach (var error in errors ?? [])
        {
            modelState.AddModelError(string.IsNullOrEmpty(error.Field) ? string.Empty : "CalendarEvent." + error.Field, error.Message);
        }
    }
}
