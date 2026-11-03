using System.Globalization;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Builds the values the calendar workflow events put in the workflow input and the tasks put in the output, as plain
/// dictionaries so Liquid and JavaScript read them without extra registration.
/// </summary>
public static class CalendarWorkflowInput
{
    /// <summary>
    /// The input that holds the event, as a dictionary.
    /// </summary>
    public const string CalendarEvent = "CalendarEvent";

    /// <summary>
    /// Builds the workflow input of a committed change.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <returns>The input.</returns>
    public static Dictionary<string, object> FromChange(CalendarEventChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var input = Base(change.Event, change.Calendar);

        input["Kind"] = change.Kind.ToString();
        input["Actor"] = change.Actor;
        input["Reason"] = change.Reason;
        input["OccurrenceStartUtc"] = change.OccurrenceStartUtc.HasValue ? Format(change.OccurrenceStartUtc.Value) : null;
        input["Previous"] = change.Previous is null ? null : ToDictionary(change.Previous);

        if (change.Attendee is not null)
        {
            input["Attendee"] = ToDictionary(change.Attendee);
            input["AttendeeEmail"] = change.Attendee.Email;
            input["AttendeeUserId"] = change.Attendee.UserId;
            input["AttendeeResponse"] = change.Attendee.Response.ToString();
        }

        return input;
    }

    /// <summary>
    /// Builds the workflow input of a reminder.
    /// </summary>
    /// <param name="context">The reminder.</param>
    /// <returns>The input.</returns>
    public static Dictionary<string, object> FromReminder(CalendarReminderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var input = Base(context.Occurrence.Event, context.Calendar);

        input["OccurrenceStartUtc"] = Format(context.Occurrence.StartUtc);
        input["OccurrenceEndUtc"] = Format(context.Occurrence.EndUtc);
        input["MinutesBefore"] = (int)context.Offset.TotalMinutes;

        return input;
    }

    /// <summary>
    /// Converts an event to a dictionary.
    /// </summary>
    /// <param name="calendarEvent">The event.</param>
    /// <returns>The dictionary.</returns>
    public static Dictionary<string, object> ToDictionary(CalendarEvent calendarEvent)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        return new Dictionary<string, object>
        {
            ["Id"] = calendarEvent.ItemId,
            ["CalendarId"] = calendarEvent.CalendarId,
            ["EventType"] = calendarEvent.EventType,
            ["Title"] = calendarEvent.Title,
            ["Description"] = calendarEvent.Description,
            ["Location"] = calendarEvent.Location,
            ["AllDay"] = calendarEvent.AllDay,
            ["Start"] = calendarEvent.Start.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            ["End"] = calendarEvent.End.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            ["TimeZoneId"] = calendarEvent.TimeZoneId,
            ["StartUtc"] = Format(calendarEvent.StartUtc),
            ["EndUtc"] = Format(calendarEvent.EndUtc),
            ["Status"] = calendarEvent.Status.ToString(),
            ["Transparency"] = calendarEvent.Transparency.ToString(),
            ["Recurrence"] = calendarEvent.Recurrence?.Rule,
            ["SeriesId"] = calendarEvent.SeriesId,
            ["Source"] = calendarEvent.Source,
            ["SourceId"] = calendarEvent.SourceId,
            ["Attendees"] = calendarEvent.Attendees.Select(ToDictionary).ToList(),
        };
    }

    /// <summary>
    /// Converts an attendee to a dictionary.
    /// </summary>
    /// <param name="attendee">The attendee.</param>
    /// <returns>The dictionary.</returns>
    public static Dictionary<string, object> ToDictionary(CalendarAttendee attendee)
    {
        ArgumentNullException.ThrowIfNull(attendee);

        return new Dictionary<string, object>
        {
            ["Name"] = attendee.Name,
            ["Email"] = attendee.Email,
            ["UserId"] = attendee.UserId,
            ["ResourceCalendarId"] = attendee.ResourceCalendarId,
            ["Role"] = attendee.Role.ToString(),
            ["Response"] = attendee.Response.ToString(),
        };
    }

    /// <summary>
    /// Formats a UTC time as ISO 8601.
    /// </summary>
    /// <param name="utc">The time.</param>
    /// <returns>The text.</returns>
    public static string Format(DateTime utc)
    {
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, object> Base(CalendarEvent calendarEvent, Calendar calendar)
    {
        return new Dictionary<string, object>
        {
            [CalendarEvent] = ToDictionary(calendarEvent),
            ["EventId"] = calendarEvent.ItemId,
            ["CalendarId"] = calendarEvent.CalendarId,
            ["CalendarName"] = calendar?.Name,
            ["CalendarType"] = calendar?.Type,
            ["EventType"] = calendarEvent.EventType,
            ["Title"] = calendarEvent.Title,
            ["StartUtc"] = Format(calendarEvent.StartUtc),
            ["EndUtc"] = Format(calendarEvent.EndUtc),
            ["Source"] = calendarEvent.Source,
            ["SourceId"] = calendarEvent.SourceId,
        };
    }
}
