using System.Globalization;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Workflows;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Applies <see cref="CalendarEventInput"/> to an event, the same way for the API and the AI tools.
/// </summary>
public static class CalendarEventInputApplier
{
    /// <summary>
    /// Applies the fields that are set.
    /// </summary>
    /// <param name="input">The fields.</param>
    /// <param name="target">The event.</param>
    /// <param name="errors">Fields that could not be read.</param>
    public static void Apply(CalendarEventInput input, CalendarEvent target, IList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(errors);

        target.Title = input.Title ?? target.Title;
        target.Description = input.Description ?? target.Description;
        target.Location = input.Location ?? target.Location;
        target.EventType = input.EventType ?? target.EventType;
        target.TimeZoneId = input.TimeZoneId ?? target.TimeZoneId;

        if (input.Status is not null && Enum.TryParse<CalendarEventStatus>(input.Status, ignoreCase: true, out var status))
        {
            target.Status = status;
        }

        if (input.Transparency is not null && Enum.TryParse<CalendarTransparency>(input.Transparency, ignoreCase: true, out var transparency))
        {
            target.Transparency = transparency;
        }

        if (input.Recurrence is not null)
        {
            target.Recurrence = string.IsNullOrWhiteSpace(input.Recurrence) ? null : new CalendarRecurrence { Rule = input.Recurrence.Trim() };
        }

        if (input.Attendees is not null)
        {
            target.Attendees = CalendarWorkflowService.ParseAttendees(string.Join('\n', input.Attendees));
        }

        var allDay = input.AllDay ?? (input.Start is { Length: 10 } || target.AllDay);

        if (input.Start is null && input.End is null)
        {
            target.AllDay = allDay && target.AllDay;

            return;
        }

        if (allDay)
        {
            if (!TryParseDate(input.Start, out var startDate) && input.Start is not null)
            {
                errors.Add("Start must be a date (yyyy-MM-dd) for an all-day event.");

                return;
            }

            var first = input.Start is null ? target.Start.Date : startDate;
            var last = TryParseDate(input.End, out var endDate) ? endDate : first;

            target.AllDay = true;
            target.Start = first;
            target.End = (last < first ? first : last).AddDays(1);

            return;
        }

        var length = target.End - target.Start;

        if (input.Start is not null)
        {
            if (!CalendarWorkflowService.TryParseTime(input.Start, target.TimeZoneId, out var start))
            {
                errors.Add("Start is not an ISO 8601 time.");

                return;
            }

            target.Start = start;
        }

        if (input.End is not null)
        {
            if (!CalendarWorkflowService.TryParseTime(input.End, target.TimeZoneId, out var end))
            {
                errors.Add("End is not an ISO 8601 time.");

                return;
            }

            target.End = end;
        }
        else
        {
            target.End = target.Start + (length > TimeSpan.Zero && !target.AllDay ? length : TimeSpan.FromHours(1));
        }

        target.AllDay = false;
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
