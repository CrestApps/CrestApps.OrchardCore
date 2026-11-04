using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// A service level policy that names a business hours calendar counts only the calendar's open hours.
/// </summary>
public sealed class SlaBusinessHoursTests
{
    private const string Zone = "America/New_York";

    [Fact]
    public async Task DueAsync_AFourHourTargetStartedFridayAtFour_OnWeekdayNineToFiveHours_IsDueMondayAtNoon()
    {
        // Arrange: Friday 6 November 2026 at 16:00 in New York (EST, UTC-5).
        var clock = CreateClock(WeekdaysNineToFive());
        var startUtc = new DateTime(2026, 11, 6, 21, 0, 0, DateTimeKind.Utc);

        // Act
        var due = await clock.DueAsync(startUtc, 240, new ServiceLevelPolicy { HoursCalendarId = "weekdays" }, TestContext.Current.CancellationToken);

        // Assert: one hour on Friday, three on Monday 9 November from 09:00, so 12:00 EST.
        Assert.Equal(new DateTime(2026, 11, 9, 17, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public async Task DueAsync_SkipsTheCalendarsHolidays()
    {
        // Arrange: Monday 9 November is a holiday, so the three hours left run on Tuesday.
        var calendar = WeekdaysNineToFive();

        calendar.Holidays.Add(new DateOnly(2026, 11, 9));

        var clock = CreateClock(calendar);
        var startUtc = new DateTime(2026, 11, 6, 21, 0, 0, DateTimeKind.Utc);

        // Act
        var due = await clock.DueAsync(startUtc, 240, new ServiceLevelPolicy { HoursCalendarId = "weekdays" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new DateTime(2026, 11, 10, 17, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public async Task DueAsync_WhenTheCalendarIsDisabled_CountsAroundTheClock()
    {
        // Arrange
        var calendar = WeekdaysNineToFive();

        calendar.Enabled = false;

        var clock = CreateClock(calendar);
        var startUtc = new DateTime(2026, 11, 6, 21, 0, 0, DateTimeKind.Utc);

        // Act
        var due = await clock.DueAsync(startUtc, 240, new ServiceLevelPolicy { HoursCalendarId = "weekdays" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(startUtc.AddHours(4), due);
    }

    [Fact]
    public void GetOpenWindows_AnOvernightWindow_RunsIntoTheNextDay_AndSpringForwardShortensIt()
    {
        // Arrange: open Saturday 22:00 to Sunday 06:00 in New York, over the night clocks jump forward (14 March 2027).
        var calendar = new BusinessHoursCalendar
        {
            TimeZoneId = Zone,
            WeeklySchedule = [new BusinessHoursDay { Day = DayOfWeek.Saturday, IsOpen = true, OpenMinute = 22 * 60, CloseMinute = 6 * 60 }],
        };
        var fromUtc = new DateTime(2027, 3, 13, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var windows = DefaultBusinessHoursService.GetOpenWindows(calendar, fromUtc, fromUtc.AddDays(2), null);

        // Assert: 22:00 EST is 03:00 UTC on Sunday; 06:00 EDT is 10:00 UTC, seven hours later.
        var window = Assert.Single(windows);

        Assert.Equal(new DateTime(2027, 3, 14, 3, 0, 0, DateTimeKind.Utc), window.StartUtc);
        Assert.Equal(new DateTime(2027, 3, 14, 10, 0, 0, DateTimeKind.Utc), window.EndUtc);
    }

    private static SlaClock CreateClock(BusinessHoursCalendar calendar)
    {
        var manager = new Mock<IBusinessHoursCalendarManager>();

        manager.Setup(m => m.FindByIdAsync(calendar.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(calendar);

        return new SlaClock([new BusinessHoursServiceHoursProvider(manager.Object)]);
    }

    private static BusinessHoursCalendar WeekdaysNineToFive()
    {
        return new BusinessHoursCalendar
        {
            ItemId = "weekdays",
            Name = "Weekdays",
            TimeZoneId = Zone,
            WeeklySchedule =
            [
                .. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                    .Select(day => new BusinessHoursDay { Day = day, IsOpen = true, OpenMinute = 9 * 60, CloseMinute = 17 * 60 }),
            ],
        };
    }
}
