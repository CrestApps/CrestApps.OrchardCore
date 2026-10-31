using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Core.Recurrence;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// Writing and reading iCalendar documents.
/// </summary>
public sealed class ICalendarTests
{
    private static readonly DateTime _now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WriteThenRead_KeepsTimesRuleExceptionsAndAttendees()
    {
        // Arrange
        var series = new CalendarEvent
        {
            ItemId = "series1",
            Uid = "series1@example.com",
            Title = "Stand-up; daily, short",
            Description = "Line one\nLine two",
            Location = "Room 1",
            TimeZoneId = "America/New_York",
            Start = new DateTime(2026, 11, 2, 9, 0, 0),
            End = new DateTime(2026, 11, 2, 9, 15, 0),
            Recurrence = new CalendarRecurrence { Rule = "FREQ=DAILY;COUNT=5", ExceptionDates = [new DateTime(2026, 11, 4, 9, 0, 0)] },
            Attendees = [new CalendarAttendee { Email = "jane@contoso.com", Name = "Jane Doe", Response = CalendarAttendeeResponse.Accepted }],
        };

        CalendarEventExpander.ComputeTimes(series);

        // Act
        var text = ICalendarWriter.Write("Team", [series], _now);
        var read = Assert.Single(ICalendarReader.Read(text, "UTC"));

        // Assert
        Assert.Contains("BEGIN:VTIMEZONE", text, StringComparison.Ordinal);
        Assert.Contains("DTSTART;TZID=America/New_York:20261102T090000", text, StringComparison.Ordinal);
        Assert.Equal("series1@example.com", read.Uid);
        Assert.Equal("Stand-up; daily, short", read.Summary);
        Assert.Equal("Line one\nLine two", read.Description);
        Assert.Equal("America/New_York", read.TimeZoneId);
        Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0), read.Start);
        Assert.Equal(new DateTime(2026, 11, 2, 9, 15, 0), read.End);
        Assert.Equal("FREQ=DAILY;COUNT=5", read.Rule);
        Assert.Equal([new DateTime(2026, 11, 4, 9, 0, 0)], read.ExceptionDates);
        Assert.Equal(CalendarAttendeeResponse.Accepted, Assert.Single(read.Attendees).Response);
    }

    [Fact]
    public void Read_HandlesAllDayUtcFoldedLinesAndOverrides()
    {
        // Arrange
        var text = string.Join("\r\n",
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "BEGIN:VEVENT",
            "UID:holiday",
            "DTSTART;VALUE=DATE:20261225",
            "DTEND;VALUE=DATE:20261226",
            "SUMMARY:Christmas",
            "END:VEVENT",
            "BEGIN:VEVENT",
            "UID:call",
            "DTSTART:20261103T150000Z",
            "DURATION:PT30M",
            "SUMMARY:A very long summary that is folded onto a",
            "  second line",
            "END:VEVENT",
            "BEGIN:VEVENT",
            "UID:weekly",
            "RECURRENCE-ID;TZID=Europe/London:20261110T100000",
            "DTSTART;TZID=Europe/London:20261110T110000",
            "DTEND;TZID=Europe/London:20261110T120000",
            "SUMMARY:Moved",
            "END:VEVENT",
            "END:VCALENDAR");

        // Act
        var events = ICalendarReader.Read(text, "America/New_York");

        // Assert
        var holiday = events.Single(item => item.Uid == "holiday");
        Assert.True(holiday.AllDay);
        Assert.Equal(new DateTime(2026, 12, 26), holiday.End);

        var call = events.Single(item => item.Uid == "call");
        Assert.Equal(new DateTime(2026, 11, 3, 10, 0, 0), call.Start);
        Assert.Equal(new DateTime(2026, 11, 3, 10, 30, 0), call.End);
        Assert.Equal("A very long summary that is folded onto a second line", call.Summary);

        var moved = events.Single(item => item.Uid == "weekly");
        Assert.Equal(new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc), moved.RecurrenceIdUtc);
        Assert.Equal("Europe/London", moved.TimeZoneId);
    }

    [Fact]
    public void Write_FoldsLongLinesAt75Octets()
    {
        // Arrange
        var calendarEvent = new CalendarEvent
        {
            ItemId = "long",
            Title = new string('x', 200),
            TimeZoneId = "UTC",
            Start = new DateTime(2026, 11, 2, 9, 0, 0),
            End = new DateTime(2026, 11, 2, 10, 0, 0),
        };

        CalendarEventExpander.ComputeTimes(calendarEvent);

        // Act
        var text = ICalendarWriter.Write(null, [calendarEvent], _now);

        // Assert
        Assert.All(text.Split("\r\n"), line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= 75));
        Assert.Equal(new string('x', 200), ICalendarReader.Read(text, "UTC")[0].Summary);
    }
}
