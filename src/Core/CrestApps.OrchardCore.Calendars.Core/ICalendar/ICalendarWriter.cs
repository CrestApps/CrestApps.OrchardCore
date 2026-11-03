using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Calendars.Models;
using NodaTime;

namespace CrestApps.OrchardCore.Calendars.Core.ICalendar;

/// <summary>
/// Writes events as an RFC 5545 iCalendar document: one-time events, repeating events with their rule and exception dates,
/// changed occurrences with a <c>RECURRENCE-ID</c>, attendees, and a <c>VTIMEZONE</c> for each time zone used.
/// </summary>
public static class ICalendarWriter
{
    private const string ProductId = "-//CrestApps//Orchard Core Calendars//EN";

    /// <summary>
    /// Writes a calendar document.
    /// </summary>
    /// <param name="calendarName">The calendar's name, written as <c>X-WR-CALNAME</c>, or <see langword="null"/>.</param>
    /// <param name="events">The events, including overrides of repeating events.</param>
    /// <param name="nowUtc">The current time, written as each event's <c>DTSTAMP</c>.</param>
    /// <param name="method">The iTIP method, such as <c>PUBLISH</c>, <c>REQUEST</c> or <c>CANCEL</c>.</param>
    /// <returns>The document.</returns>
    public static string Write(string calendarName, IEnumerable<CalendarEvent> events, DateTime nowUtc, string method = "PUBLISH")
    {
        ArgumentNullException.ThrowIfNull(events);

        var list = events.ToList();
        var builder = new StringBuilder();

        Line(builder, "BEGIN:VCALENDAR");
        Line(builder, "VERSION:2.0");
        Line(builder, "PRODID:" + ProductId);
        Line(builder, "CALSCALE:GREGORIAN");
        Line(builder, "METHOD:" + method);

        if (!string.IsNullOrWhiteSpace(calendarName))
        {
            Line(builder, "X-WR-CALNAME:" + Escape(calendarName));
        }

        foreach (var zoneId in list.Where(calendarEvent => !calendarEvent.AllDay).Select(calendarEvent => CalendarTime.NormalizeZoneId(calendarEvent.TimeZoneId)).Where(id => id is not null && id != "UTC").Distinct())
        {
            var years = list.Where(calendarEvent => CalendarTime.NormalizeZoneId(calendarEvent.TimeZoneId) == zoneId).Select(calendarEvent => calendarEvent.Start.Year).ToList();

            WriteTimeZone(builder, zoneId, Math.Min(years.Min(), nowUtc.Year) - 1, Math.Max(years.Max(), nowUtc.Year) + 5);
        }

        foreach (var calendarEvent in list)
        {
            WriteEvent(builder, calendarEvent, nowUtc);
        }

        Line(builder, "END:VCALENDAR");

        return builder.ToString();
    }

    private static void WriteEvent(StringBuilder builder, CalendarEvent calendarEvent, DateTime nowUtc)
    {
        var zoneId = CalendarTime.NormalizeZoneId(calendarEvent.TimeZoneId) ?? "UTC";

        Line(builder, "BEGIN:VEVENT");
        Line(builder, "UID:" + Escape(calendarEvent.Uid ?? calendarEvent.ItemId));
        Line(builder, "DTSTAMP:" + Utc(nowUtc));

        if (calendarEvent.AllDay)
        {
            Line(builder, "DTSTART;VALUE=DATE:" + calendarEvent.Start.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            Line(builder, "DTEND;VALUE=DATE:" + calendarEvent.End.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        }
        else if (zoneId == "UTC")
        {
            Line(builder, "DTSTART:" + Utc(calendarEvent.StartUtc));
            Line(builder, "DTEND:" + Utc(calendarEvent.EndUtc));
        }
        else
        {
            Line(builder, $"DTSTART;TZID={zoneId}:{Local(calendarEvent.Start)}");
            Line(builder, $"DTEND;TZID={zoneId}:{Local(calendarEvent.End)}");
        }

        if (calendarEvent.SeriesId is not null && calendarEvent.OriginalStartUtc.HasValue)
        {
            var original = calendarEvent.OriginalStartUtc.Value;

            Line(builder, calendarEvent.AllDay
                ? "RECURRENCE-ID;VALUE=DATE:" + CalendarTime.ToLocal(original, zoneId).ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                : zoneId == "UTC" ? "RECURRENCE-ID:" + Utc(original) : $"RECURRENCE-ID;TZID={zoneId}:{Local(CalendarTime.ToLocal(original, zoneId))}");
        }

        if (calendarEvent.Recurrence is not null && !string.IsNullOrWhiteSpace(calendarEvent.Recurrence.Rule))
        {
            Line(builder, "RRULE:" + calendarEvent.Recurrence.Rule);

            foreach (var exception in calendarEvent.Recurrence.ExceptionDates)
            {
                Line(builder, calendarEvent.AllDay
                    ? "EXDATE;VALUE=DATE:" + exception.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                    : zoneId == "UTC" ? "EXDATE:" + Utc(exception) : $"EXDATE;TZID={zoneId}:{Local(exception)}");
            }
        }

        Line(builder, "SUMMARY:" + Escape(calendarEvent.Title));

        if (!string.IsNullOrWhiteSpace(calendarEvent.Description))
        {
            Line(builder, "DESCRIPTION:" + Escape(calendarEvent.Description));
        }

        if (!string.IsNullOrWhiteSpace(calendarEvent.Location))
        {
            Line(builder, "LOCATION:" + Escape(calendarEvent.Location));
        }

        Line(builder, "STATUS:" + calendarEvent.Status switch
        {
            CalendarEventStatus.Tentative => "TENTATIVE",
            CalendarEventStatus.Cancelled => "CANCELLED",
            _ => "CONFIRMED",
        });
        Line(builder, "TRANSP:" + (calendarEvent.Transparency == CalendarTransparency.Free ? "TRANSPARENT" : "OPAQUE"));
        Line(builder, "SEQUENCE:" + calendarEvent.Sequence.ToString(CultureInfo.InvariantCulture));

        if (calendarEvent.Visibility == CalendarEventVisibility.Private)
        {
            Line(builder, "CLASS:PRIVATE");
        }

        if (calendarEvent.ModifiedUtc != default)
        {
            Line(builder, "LAST-MODIFIED:" + Utc(calendarEvent.ModifiedUtc));
        }

        foreach (var attendee in calendarEvent.Attendees.Where(attendee => !string.IsNullOrWhiteSpace(attendee.Email)))
        {
            var name = string.IsNullOrWhiteSpace(attendee.Name) ? string.Empty : ";CN=" + Quote(attendee.Name);
            var role = attendee.Role switch
            {
                CalendarAttendeeRole.Optional => "OPT-PARTICIPANT",
                CalendarAttendeeRole.Organizer => "CHAIR",
                _ => "REQ-PARTICIPANT",
            };
            var status = attendee.Response switch
            {
                CalendarAttendeeResponse.Accepted => "ACCEPTED",
                CalendarAttendeeResponse.Declined => "DECLINED",
                CalendarAttendeeResponse.Tentative => "TENTATIVE",
                _ => "NEEDS-ACTION",
            };

            Line(builder, $"ATTENDEE{name};ROLE={role};PARTSTAT={status}:mailto:{attendee.Email.Trim()}");
        }

        Line(builder, "END:VEVENT");
    }

    private static void WriteTimeZone(StringBuilder builder, string zoneId, int fromYear, int toYear)
    {
        var zone = CalendarTime.GetZone(zoneId);
        var start = Instant.FromUtc(Math.Max(1900, fromYear), 1, 1, 0, 0);
        var end = Instant.FromUtc(Math.Min(9998, toYear), 12, 31, 0, 0);

        Line(builder, "BEGIN:VTIMEZONE");
        Line(builder, "TZID:" + zoneId);

        var intervals = zone.GetZoneIntervals(start, end).ToList();

        for (var index = 0; index < intervals.Count; index++)
        {
            var interval = intervals[index];
            var previous = index > 0 ? intervals[index - 1].WallOffset : interval.WallOffset;
            var kind = interval.Savings != Offset.Zero ? "DAYLIGHT" : "STANDARD";
            var localStart = interval.HasStart && interval.Start > start
                ? interval.Start.WithOffset(previous).LocalDateTime
                : start.WithOffset(interval.WallOffset).LocalDateTime;

            Line(builder, "BEGIN:" + kind);
            Line(builder, "DTSTART:" + localStart.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture));
            Line(builder, "TZOFFSETFROM:" + FormatOffset(previous));
            Line(builder, "TZOFFSETTO:" + FormatOffset(interval.WallOffset));

            if (!string.IsNullOrEmpty(interval.Name))
            {
                Line(builder, "TZNAME:" + Escape(interval.Name));
            }

            Line(builder, "END:" + kind);
        }

        Line(builder, "END:VTIMEZONE");
    }

    private static string FormatOffset(Offset offset)
    {
        var span = offset.ToTimeSpan();

        return (span < TimeSpan.Zero ? "-" : "+") + span.Duration().ToString("hhmm", CultureInfo.InvariantCulture);
    }

    private static string Utc(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    }

    private static string Local(DateTime value)
    {
        return value.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Escapes text for a property value: backslashes, semicolons, commas and new lines.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The escaped text.</returns>
    public static string Escape(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "'", StringComparison.Ordinal) + "\"";
    }

    // Lines longer than 75 octets are folded onto continuation lines that start with a space, never inside a character.
    private static void Line(StringBuilder builder, string line)
    {
        var bytes = 0;
        var start = 0;
        var index = 0;

        while (index < line.Length)
        {
            var length = char.IsHighSurrogate(line[index]) && index + 1 < line.Length ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(line.AsSpan(index, length));

            if (bytes + size > (start == 0 ? 75 : 74))
            {
                builder.Append(line, start, index - start).Append("\r\n ");
                start = index;
                bytes = 0;
            }

            bytes += size;
            index += length;
        }

        builder.Append(line, start, line.Length - start).Append("\r\n");
    }
}
