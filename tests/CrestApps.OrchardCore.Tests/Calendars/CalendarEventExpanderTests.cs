using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Core.Recurrence;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// Expanding events into occurrences across time zones and daylight-saving changes.
/// </summary>
public sealed class CalendarEventExpanderTests
{
    [Fact]
    public void WeeklyMeeting_KeepsItsWallClockTime_AcrossTheDaylightSavingChange()
    {
        // Arrange (New York leaves daylight saving on 2026-11-01)
        var meeting = Event("America/New_York", new DateTime(2026, 10, 26, 9, 0, 0), new DateTime(2026, 10, 26, 10, 0, 0), "FREQ=WEEKLY;COUNT=2");

        // Act
        var occurrences = CalendarEventExpander.Expand(meeting, Utc(2026, 10, 1), Utc(2026, 12, 1)).ToList();

        // Assert
        Assert.Equal(2, occurrences.Count);
        Assert.Equal(Utc(2026, 10, 26, 13), occurrences[0].StartUtc);
        Assert.Equal(Utc(2026, 11, 2, 14), occurrences[1].StartUtc);
        Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0), occurrences[1].Start);
        Assert.Equal(TimeSpan.FromHours(1), occurrences[1].EndUtc - occurrences[1].StartUtc);
    }

    [Fact]
    public void DailyEvent_AtASkippedTime_MovesForwardByTheGap()
    {
        // Arrange (London skips 01:00-02:00 on 2027-03-28)
        var calendarEvent = Event("Europe/London", new DateTime(2027, 3, 27, 1, 30, 0), new DateTime(2027, 3, 27, 2, 0, 0), "FREQ=DAILY;COUNT=2");

        // Act
        var occurrences = CalendarEventExpander.Expand(calendarEvent, Utc(2027, 3, 1), Utc(2027, 4, 1)).ToList();

        // Assert (01:30 doesn't exist, so it becomes 02:30 BST, which is 01:30 UTC)
        Assert.Equal(Utc(2027, 3, 28, 1, 30), occurrences[1].StartUtc);
    }

    [Fact]
    public void DailyEvent_AtARepeatedTime_TakesTheEarlierMoment()
    {
        // Arrange (London repeats 01:00-02:00 on 2026-10-25)
        var calendarEvent = Event("Europe/London", new DateTime(2026, 10, 24, 1, 30, 0), new DateTime(2026, 10, 24, 1, 45, 0), "FREQ=DAILY;COUNT=2");

        // Act
        var occurrences = CalendarEventExpander.Expand(calendarEvent, Utc(2026, 10, 1), Utc(2026, 11, 1)).ToList();

        // Assert (the first 01:30 is still BST, so 00:30 UTC)
        Assert.Equal(Utc(2026, 10, 25, 0, 30), occurrences[1].StartUtc);
    }

    [Fact]
    public void AllDaySeries_SpansWholeLocalDays()
    {
        // Arrange
        var calendarEvent = Event("America/Chicago", new DateTime(2026, 12, 24), new DateTime(2026, 12, 26), "FREQ=YEARLY;COUNT=2");
        calendarEvent.AllDay = true;
        CalendarEventExpander.ComputeTimes(calendarEvent);

        // Act
        var occurrences = CalendarEventExpander.Expand(calendarEvent, Utc(2027, 1, 1), Utc(2028, 1, 1)).ToList();

        // Assert
        var occurrence = Assert.Single(occurrences);
        Assert.Equal(new DateTime(2027, 12, 24), occurrence.Start);
        Assert.Equal(new DateTime(2027, 12, 26), occurrence.End);
        Assert.Equal(Utc(2027, 12, 24, 6), occurrence.StartUtc);
    }

    [Fact]
    public void ExceptionDatesAndOverrides_AreLeftOut()
    {
        // Arrange
        var calendarEvent = Event("UTC", new DateTime(2026, 11, 2, 9, 0, 0), new DateTime(2026, 11, 2, 9, 30, 0), "FREQ=DAILY;COUNT=5");
        calendarEvent.Recurrence.ExceptionDates.Add(new DateTime(2026, 11, 3, 9, 0, 0));
        var overridden = new HashSet<DateTime> { Utc(2026, 11, 4, 9) };

        // Act
        var starts = CalendarEventExpander.Expand(calendarEvent, Utc(2026, 11, 1), Utc(2026, 12, 1), overridden).Select(occurrence => occurrence.StartUtc.Day).ToList();

        // Assert
        Assert.Equal([2, 5, 6], starts);
    }

    [Fact]
    public void ComputeTimes_SetsTheSeriesEnd_OnlyForAFiniteSeries()
    {
        // Arrange
        var finite = Event("UTC", new DateTime(2026, 11, 2, 9, 0, 0), new DateTime(2026, 11, 2, 10, 0, 0), "FREQ=DAILY;COUNT=3");
        var endless = Event("UTC", new DateTime(2026, 11, 2, 9, 0, 0), new DateTime(2026, 11, 2, 10, 0, 0), "FREQ=DAILY");

        // Assert
        Assert.Equal(Utc(2026, 11, 4, 10), finite.Recurrence.EndUtc);
        Assert.Null(endless.Recurrence.EndUtc);
        Assert.Equal(DateTime.MaxValue, CalendarEventExpander.GetRangeEndUtc(endless));
    }

    [Fact]
    public void Expand_ReturnsOnlyOccurrencesThatOverlapTheRange()
    {
        // Arrange
        var calendarEvent = Event("UTC", new DateTime(2026, 1, 1, 9, 0, 0), new DateTime(2026, 1, 1, 10, 0, 0), "FREQ=DAILY");

        // Act
        var occurrences = CalendarEventExpander.Expand(calendarEvent, Utc(2026, 6, 10, 9, 30), Utc(2026, 6, 12)).ToList();

        // Assert
        Assert.Equal([Utc(2026, 6, 10, 9), Utc(2026, 6, 11, 9)], occurrences.Select(occurrence => occurrence.StartUtc));
        Assert.All(occurrences, occurrence => Assert.True(occurrence.IsRecurring));
    }

    [Fact]
    public void ToUtc_AcceptsWindowsTimeZoneIds()
    {
        // Act
        var utc = CalendarTime.ToUtc(new DateTime(2026, 7, 1, 12, 0, 0), "Pacific Standard Time");

        // Assert
        Assert.Equal(Utc(2026, 7, 1, 19), utc);
    }

    private static CalendarEvent Event(string timeZoneId, DateTime start, DateTime end, string rule)
    {
        var calendarEvent = new CalendarEvent
        {
            ItemId = "event1",
            TimeZoneId = timeZoneId,
            Start = start,
            End = end,
            Recurrence = new CalendarRecurrence { Rule = rule },
        };

        CalendarEventExpander.ComputeTimes(calendarEvent);

        return calendarEvent;
    }

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }
}
