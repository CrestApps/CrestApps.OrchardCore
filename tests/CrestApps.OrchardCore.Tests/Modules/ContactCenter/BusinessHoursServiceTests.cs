using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the business-hours evaluation that gates every background-initiated send — most importantly the automated SMS
/// re-engagement (cadence) task, which must never nudge a contact after hours. A regression here would either let a
/// nudge fire outside the contact's local business hours (the exact behavior the feature exists to prevent) or wrongly
/// hold a live, in-hours send. Both the pure <see cref="DefaultBusinessHoursService.IsOpen(BusinessHoursCalendar, DateTime, string)"/>
/// branches and the service boundary the gate calls are covered.
/// </summary>
public sealed class BusinessHoursServiceTests
{
    // 2026-01-05 is a Monday (winter — America/New_York is EST, UTC-5); 2026-01-09 is the Friday, 2026-01-10 the Saturday.
    private static readonly DateOnly Monday = new(2026, 1, 5);

    // --- Pure schedule evaluation ----------------------------------------------------------------------------------

    [Fact]
    public void IsOpen_WithinTheSameDayWindow_IsOpen()
    {
        var calendar = WeekdayCalendar(DayOfWeek.Monday, openMinute: 9 * 60, closeMinute: 17 * 60);

        // Monday 12:00 UTC is inside 09:00–17:00.
        Assert.True(DefaultBusinessHoursService.IsOpen(calendar, At(Monday, 12, 0), timeZoneId: null));
    }

    [Fact]
    public void IsOpen_AfterTheWindowCloses_IsClosed()
    {
        // This is the core after-hours nudge case: the contact's window has ended for the day, so no follow-up may fire.
        var calendar = WeekdayCalendar(DayOfWeek.Monday, openMinute: 9 * 60, closeMinute: 17 * 60);

        Assert.False(DefaultBusinessHoursService.IsOpen(calendar, At(Monday, 18, 0), timeZoneId: null));
    }

    [Fact]
    public void IsOpen_BeforeTheWindowOpens_IsClosed()
    {
        var calendar = WeekdayCalendar(DayOfWeek.Monday, openMinute: 9 * 60, closeMinute: 17 * 60);

        Assert.False(DefaultBusinessHoursService.IsOpen(calendar, At(Monday, 8, 0), timeZoneId: null));
    }

    [Fact]
    public void IsOpen_OnADayWithNoOpenWindow_IsClosed()
    {
        var calendar = WeekdayCalendar(DayOfWeek.Monday, openMinute: 9 * 60, closeMinute: 17 * 60);

        // Sunday has no configured window at all.
        var sunday = new DateOnly(2026, 1, 4);

        Assert.False(DefaultBusinessHoursService.IsOpen(calendar, At(sunday, 12, 0), timeZoneId: null));
    }

    [Fact]
    public void IsOpen_OnAHoliday_IsClosedEvenInsideTheWeeklyWindow()
    {
        var calendar = WeekdayCalendar(DayOfWeek.Monday, openMinute: 9 * 60, closeMinute: 17 * 60);
        calendar.Holidays.Add(Monday);

        Assert.False(DefaultBusinessHoursService.IsOpen(calendar, At(Monday, 12, 0), timeZoneId: null));
    }

    [Fact]
    public void IsOpen_EvaluatesInTheOverriddenTimeZone()
    {
        // The calendar is authored in UTC, but a nudge evaluates it in the contact's local zone. 21:00 UTC is closed in
        // UTC yet 16:00 EST — inside the 09:00–17:00 window — so the contact-local override must report open.
        var calendar = WeekdayCalendar(DayOfWeek.Monday, openMinute: 9 * 60, closeMinute: 17 * 60);
        var instant = At(Monday, 21, 0);

        Assert.False(DefaultBusinessHoursService.IsOpen(calendar, instant, timeZoneId: null));
        Assert.True(DefaultBusinessHoursService.IsOpen(calendar, instant, timeZoneId: "America/New_York"));
    }

    [Fact]
    public void IsOpen_AcrossAnOvernightWindow_TracksBothSides()
    {
        // Friday 22:00 -> Saturday 02:00 (open minute 1320 > close minute 120).
        var calendar = WeekdayCalendar(DayOfWeek.Friday, openMinute: 22 * 60, closeMinute: 2 * 60);

        var friday = new DateOnly(2026, 1, 9);
        var saturday = new DateOnly(2026, 1, 10);

        // Late Friday is inside the window's opening side.
        Assert.True(DefaultBusinessHoursService.IsOpen(calendar, At(friday, 23, 0), timeZoneId: null));

        // Early Saturday is still inside the window that opened the night before.
        Assert.True(DefaultBusinessHoursService.IsOpen(calendar, At(saturday, 1, 0), timeZoneId: null));

        // After the overnight window closes, Saturday is closed again.
        Assert.False(DefaultBusinessHoursService.IsOpen(calendar, At(saturday, 5, 0), timeZoneId: null));
    }

    // --- Service / gate boundary ------------------------------------------------------------------------------------

    [Fact]
    public async Task Evaluate_WithNoCalendar_IsUnrestrictedSoLiveWorkIsNeverBlocked()
    {
        var service = CreateService(calendar: null);

        // An empty calendar id short-circuits before any lookup: unrestricted (null), which surfaces as "open".
        Assert.Null(await service.EvaluateAsync(string.Empty, At(Monday, 12, 0), timeZoneId: null));
        Assert.True(await service.IsOpenAsync(string.Empty, At(Monday, 12, 0), timeZoneId: null));
    }

    [Fact]
    public async Task Evaluate_WhenTheCalendarIsMissing_IsUnrestricted()
    {
        var service = CreateService(calendar: null);

        Assert.Null(await service.EvaluateAsync("missing", At(Monday, 12, 0), timeZoneId: null));
        Assert.True(await service.IsOpenAsync("missing", At(Monday, 12, 0), timeZoneId: null));
    }

    [Fact]
    public async Task Evaluate_WhenTheCalendarIsDisabled_IsUnrestricted()
    {
        var calendar = WeekdayCalendar(DayOfWeek.Monday, openMinute: 9 * 60, closeMinute: 17 * 60);
        calendar.Enabled = false;

        var service = CreateService(calendar);

        Assert.Null(await service.EvaluateAsync("calendar-1", At(Monday, 12, 0), timeZoneId: null));
        Assert.True(await service.IsOpenAsync("calendar-1", At(Monday, 12, 0), timeZoneId: null));
    }

    [Fact]
    public async Task Evaluate_WhenTheCalendarIsClosed_HoldsTheSend()
    {
        // The nudge gate calls this: a real, enabled calendar that is closed at the instant must report closed so the
        // re-engagement task backs off.
        var calendar = WeekdayCalendar(DayOfWeek.Monday, openMinute: 9 * 60, closeMinute: 17 * 60);

        var service = CreateService(calendar);

        Assert.False(await service.EvaluateAsync("calendar-1", At(Monday, 22, 0), timeZoneId: null));
        Assert.False(await service.IsOpenAsync("calendar-1", At(Monday, 22, 0), timeZoneId: null));
    }

    [Fact]
    public async Task Evaluate_WhenTheCalendarIsOpen_AllowsTheSend()
    {
        var calendar = WeekdayCalendar(DayOfWeek.Monday, openMinute: 9 * 60, closeMinute: 17 * 60);

        var service = CreateService(calendar);

        Assert.True(await service.EvaluateAsync("calendar-1", At(Monday, 12, 0), timeZoneId: null));
        Assert.True(await service.IsOpenAsync("calendar-1", At(Monday, 12, 0), timeZoneId: null));
    }

    // --- Helpers ----------------------------------------------------------------------------------------------------

    private static DefaultBusinessHoursService CreateService(BusinessHoursCalendar calendar)
    {
        var manager = new Mock<IBusinessHoursCalendarManager>();

        manager
            .Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendar);

        return new DefaultBusinessHoursService(manager.Object, new StubClock());
    }

    private static BusinessHoursCalendar WeekdayCalendar(DayOfWeek day, int openMinute, int closeMinute)
    {
        return new BusinessHoursCalendar
        {
            ItemId = "calendar-1",
            Name = "Test Calendar",
            TimeZoneId = "UTC",
            Enabled = true,
            WeeklySchedule =
            [
                new BusinessHoursDay
                {
                    Day = day,
                    IsOpen = true,
                    OpenMinute = openMinute,
                    CloseMinute = closeMinute,
                },
            ],
        };
    }

    private static DateTime At(DateOnly date, int hour, int minute)
        => new(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Utc);
}
