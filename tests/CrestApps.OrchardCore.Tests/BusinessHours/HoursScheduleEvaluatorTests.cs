using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.BusinessHours.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Calendars;

namespace CrestApps.OrchardCore.Tests.BusinessHours;

/// <summary>
/// One engine reads every hours schedule: weekly windows, windows that run past midnight, holidays and special hours,
/// in the schedule's time zone and without throwing on daylight-saving changes.
/// </summary>
public sealed class HoursScheduleEvaluatorTests
{
    // Monday 2 November 2026.
    private static readonly DateOnly _monday = new(2026, 11, 2);

    [Theory]
    [InlineData(9, 0, true)]
    [InlineData(16, 59, true)]
    [InlineData(17, 0, false)]
    [InlineData(8, 59, false)]
    public void IsOpen_FollowsTheDaysWindow(int hour, int minute, bool open)
    {
        var schedule = Weekdays(9, 17);

        Assert.Equal(open, HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, hour, minute)));
    }

    [Fact]
    public void IsOpen_ADayWithTwoWindows_IsClosedOverTheBreak()
    {
        var schedule = new HoursSchedule
        {
            Windows = [Window(DayOfWeek.Monday, 9, 12), Window(DayOfWeek.Monday, 13, 17)],
        };

        Assert.True(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 11, 0)));
        Assert.False(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 12, 30)));
        Assert.True(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 13, 0)));
    }

    [Fact]
    public void AnOvernightWindow_IsOpenBothSidesOfMidnight()
    {
        var friday = _monday.AddDays(4);
        var schedule = new HoursSchedule { Windows = [Window(DayOfWeek.Friday, 22, 6)] };

        Assert.True(HoursScheduleEvaluator.IsOpen(schedule, Utc(friday, 23, 0)));
        Assert.True(HoursScheduleEvaluator.IsOpen(schedule, Utc(friday.AddDays(1), 5, 59)));
        Assert.False(HoursScheduleEvaluator.IsOpen(schedule, Utc(friday.AddDays(1), 6, 0)));
        Assert.False(HoursScheduleEvaluator.IsOpen(schedule, Utc(friday, 21, 0)));

        var windows = HoursScheduleEvaluator.GetOpenWindows(schedule, Utc(friday, 0, 0), Utc(friday.AddDays(2), 0, 0));

        Assert.Equal([new TimeWindow(Utc(friday, 22, 0), Utc(friday.AddDays(1), 6, 0))], windows);
    }

    [Fact]
    public void AWindowThatOpensAndClosesAtTheSameMinute_IsOpenAllDay()
    {
        var schedule = new HoursSchedule { Windows = [new HoursWindow { Day = DayOfWeek.Monday, OpenMinute = 0, CloseMinute = 0 }] };

        Assert.True(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 0, 0)));
        Assert.True(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 23, 59)));
        Assert.False(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday.AddDays(1), 0, 0)));
    }

    [Fact]
    public void AHoliday_IsClosedAllDay_AndLeavesTheOtherDaysAlone()
    {
        var schedule = Weekdays(9, 17);
        schedule.DateExceptions = [new HoursDateException { Date = _monday, Name = "Holiday" }];

        Assert.False(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 12, 0)));
        Assert.True(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday.AddDays(1), 12, 0)));

        var windows = HoursScheduleEvaluator.GetOpenWindows(schedule, Utc(_monday, 0, 0), Utc(_monday.AddDays(2), 0, 0));

        Assert.Equal([new TimeWindow(Utc(_monday.AddDays(1), 9, 0), Utc(_monday.AddDays(1), 17, 0))], windows);
    }

    [Fact]
    public void AHoliday_ClosesTheEarlyHoursOfAnOvernightWindow_ThatRunIntoIt()
    {
        var friday = _monday.AddDays(4);
        var schedule = new HoursSchedule
        {
            Windows = [Window(DayOfWeek.Friday, 22, 6)],
            DateExceptions = [new HoursDateException { Date = friday.AddDays(1) }],
        };

        Assert.True(HoursScheduleEvaluator.IsOpen(schedule, Utc(friday, 23, 0)));
        Assert.False(HoursScheduleEvaluator.IsOpen(schedule, Utc(friday.AddDays(1), 1, 0)));
    }

    [Fact]
    public void SpecialHours_ReplaceTheWeeklyWindowsOnTheirDate()
    {
        var schedule = Weekdays(9, 17);
        schedule.DateExceptions = [new HoursDateException { Date = _monday, Closed = false, OpenMinute = 10 * 60, CloseMinute = 14 * 60 }];

        Assert.False(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 9, 30)));
        Assert.True(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 13, 0)));
        Assert.False(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 15, 0)));
    }

    [Fact]
    public void TheScheduleIsReadInItsTimeZone_OrInTheOverride()
    {
        var schedule = Weekdays(9, 17);
        schedule.TimeZoneId = "America/New_York";

        // 13:00 UTC is 08:00 in New York in November.
        Assert.False(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 13, 0)));
        Assert.True(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 15, 0)));
        Assert.True(HoursScheduleEvaluator.IsOpen(schedule, Utc(_monday, 13, 0), "Europe/London"));
    }

    [Fact]
    public void AnOpeningTimeSkippedByDaylightSaving_MovesForwardByTheGap()
    {
        // Clocks in New York jump from 02:00 to 03:00 on Sunday 8 March 2026; 02:30 doesn't exist and moves forward by the
        // gap to 03:30.
        var sunday = new DateOnly(2026, 3, 8);
        var schedule = new HoursSchedule
        {
            TimeZoneId = "America/New_York",
            Windows = [new HoursWindow { Day = DayOfWeek.Sunday, OpenMinute = 150, CloseMinute = 6 * 60 }],
        };

        var windows = HoursScheduleEvaluator.GetOpenWindows(schedule, Utc(sunday, 0, 0), Utc(sunday.AddDays(1), 0, 0));

        // 03:30 EDT is 07:30 UTC; 06:00 EDT is 10:00 UTC.
        Assert.Equal([new TimeWindow(Utc(sunday, 7, 30), Utc(sunday, 10, 0))], windows);
    }

    [Fact]
    public void ATimeThatHappensTwice_IsTheEarlierOfTheTwo()
    {
        // Clocks in New York fall back from 02:00 to 01:00 on Sunday 1 November 2026; 01:30 happens twice.
        var sunday = new DateOnly(2026, 11, 1);
        var schedule = new HoursSchedule
        {
            TimeZoneId = "America/New_York",
            Windows = [new HoursWindow { Day = DayOfWeek.Sunday, OpenMinute = 90, CloseMinute = 4 * 60 }],
        };

        var windows = HoursScheduleEvaluator.GetOpenWindows(schedule, Utc(sunday, 0, 0), Utc(sunday.AddDays(1), 0, 0));

        // 01:30 EDT is 05:30 UTC; 04:00 EST is 09:00 UTC.
        Assert.Equal([new TimeWindow(Utc(sunday, 5, 30), Utc(sunday, 9, 0))], windows);
    }

    [Fact]
    public void GetOpenWindows_ClipsToTheRange_AndMergesWindowsThatTouch()
    {
        var schedule = new HoursSchedule
        {
            Windows = [Window(DayOfWeek.Monday, 9, 12), Window(DayOfWeek.Monday, 12, 17)],
        };

        var windows = HoursScheduleEvaluator.GetOpenWindows(schedule, Utc(_monday, 10, 0), Utc(_monday, 16, 0));

        Assert.Equal([new TimeWindow(Utc(_monday, 10, 0), Utc(_monday, 16, 0))], windows);
    }

    [Fact]
    public async Task TheOpenHoursService_AnswersFromTheFirstSourceThatKnowsTheSchedule()
    {
        var service = new DefaultOpenHoursService([new Source("a", "Support", Weekdays(9, 17)), new Source("a", "Shadow", new HoursSchedule())]);

        Assert.True(await service.IsOpenAsync("a", Utc(_monday, 10, 0), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await service.IsOpenAsync("unknown", Utc(_monday, 10, 0), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await service.IsOpenAsync(null, Utc(_monday, 10, 0), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await service.GetOpenWindowsAsync("unknown", Utc(_monday, 0, 0), Utc(_monday, 23, 0), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal([new HoursScheduleOption("a", "Support")], await service.ListSchedulesAsync(TestContext.Current.CancellationToken));
    }

    private static HoursSchedule Weekdays(int openHour, int closeHour)
        => new()
        {
            Windows = [.. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                .Select(day => Window(day, openHour, closeHour))],
        };

    private static HoursWindow Window(DayOfWeek day, int openHour, int closeHour)
        => new() { Day = day, OpenMinute = openHour * 60, CloseMinute = closeHour * 60 };

    private static DateTime Utc(DateOnly date, int hour, int minute)
        => DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Utc);

    private sealed class Source(string id, string name, HoursSchedule schedule) : IHoursScheduleSource
    {
        public Task<HoursSchedule> FindAsync(string scheduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(scheduleId == id ? schedule : null);

        public Task<IReadOnlyList<HoursScheduleOption>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HoursScheduleOption>>([new HoursScheduleOption(id, name)]);
    }
}
