using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class BusinessHoursServiceTests
{
    private static readonly DateTime _mondayNoonUtc = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsOpen_WithinWeeklyWindow_ReturnsTrue()
    {
        // Arrange
        var calendar = CreateMondayNineToFive();

        // Act
        var open = DefaultBusinessHoursService.IsOpen(calendar, _mondayNoonUtc);

        // Assert
        Assert.True(open);
    }

    [Fact]
    public void IsOpen_OutsideWeeklyWindow_ReturnsFalse()
    {
        // Arrange
        var calendar = CreateMondayNineToFive();

        // Act
        var open = DefaultBusinessHoursService.IsOpen(calendar, new DateTime(2026, 1, 5, 18, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.False(open);
    }

    [Fact]
    public void IsOpen_OnClosedDay_ReturnsFalse()
    {
        // Arrange
        var calendar = CreateMondayNineToFive();

        // Act
        var open = DefaultBusinessHoursService.IsOpen(calendar, new DateTime(2026, 1, 4, 12, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.False(open);
    }

    [Fact]
    public void IsOpen_OnHoliday_ReturnsFalse()
    {
        // Arrange
        var calendar = CreateMondayNineToFive();
        calendar.Holidays = [new DateOnly(2026, 1, 5)];

        // Act
        var open = DefaultBusinessHoursService.IsOpen(calendar, _mondayNoonUtc);

        // Assert
        Assert.False(open);
    }

    [Fact]
    public void IsOpen_WithNoSchedule_ReturnsFalse()
    {
        // Arrange
        var calendar = new BusinessHoursCalendar { ItemId = "cal1", TimeZoneId = "UTC", Enabled = true };

        // Act
        var open = DefaultBusinessHoursService.IsOpen(calendar, _mondayNoonUtc);

        // Assert
        Assert.False(open);
    }

    [Fact]
    public void IsOpen_RespectsTimeZone()
    {
        // Arrange
        var calendar = CreateMondayNineToFive("America/New_York");

        // Act
        var openLocalMorning = DefaultBusinessHoursService.IsOpen(calendar, new DateTime(2026, 1, 5, 14, 30, 0, DateTimeKind.Utc));
        var closedLocalEarly = DefaultBusinessHoursService.IsOpen(calendar, new DateTime(2026, 1, 5, 13, 30, 0, DateTimeKind.Utc));

        // Assert
        Assert.True(openLocalMorning);
        Assert.False(closedLocalEarly);
    }

    [Theory]
    [InlineData(2026, 1, 5, 23, 0, true)]
    [InlineData(2026, 1, 6, 2, 0, true)]
    [InlineData(2026, 1, 6, 7, 0, false)]
    public void IsOpen_WithOvernightWindow_EvaluatesBothSidesOfMidnight(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        bool expected)
    {
        // Arrange
        var calendar = new BusinessHoursCalendar
        {
            ItemId = "cal1",
            TimeZoneId = "UTC",
            Enabled = true,
            WeeklySchedule =
            [
                new BusinessHoursDay { Day = DayOfWeek.Monday, IsOpen = true, OpenMinute = 1320, CloseMinute = 360 },
            ],
        };

        // Act
        var open = DefaultBusinessHoursService.IsOpen(
            calendar,
            new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal(expected, open);
    }

    [Fact]
    public void IsOpen_WithEqualOpenAndCloseMinutes_TreatsDayAsOpenAllDay()
    {
        // Arrange
        var calendar = new BusinessHoursCalendar
        {
            ItemId = "cal1",
            TimeZoneId = "UTC",
            Enabled = true,
            WeeklySchedule =
            [
                new BusinessHoursDay { Day = DayOfWeek.Monday, IsOpen = true, OpenMinute = 0, CloseMinute = 0 },
            ],
        };

        // Act
        var open = DefaultBusinessHoursService.IsOpen(calendar, _mondayNoonUtc);

        // Assert
        Assert.True(open);
    }

    [Fact]
    public async Task IsOpenAsync_WithEmptyCalendarId_ReturnsTrue()
    {
        // Arrange
        var service = CreateService(new Mock<IBusinessHoursCalendarManager>());

        // Act
        var open = await service.IsOpenAsync(string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(open);
    }

    [Fact]
    public async Task IsOpenAsync_WithDisabledCalendar_ReturnsTrue()
    {
        // Arrange
        var manager = new Mock<IBusinessHoursCalendarManager>();
        manager
            .Setup(m => m.FindByIdAsync("cal1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessHoursCalendar { ItemId = "cal1", Enabled = false });

        var service = CreateService(manager);

        // Act
        var open = await service.IsOpenAsync("cal1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(open);
    }

    [Fact]
    public async Task IsOpenAsync_EvaluatesScheduleThroughManager()
    {
        // Arrange
        var manager = new Mock<IBusinessHoursCalendarManager>();
        manager
            .Setup(m => m.FindByIdAsync("cal1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMondayNineToFive());

        var service = CreateService(manager, _mondayNoonUtc);

        // Act
        var open = await service.IsOpenAsync("cal1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(open);
    }

    private static DefaultBusinessHoursService CreateService(Mock<IBusinessHoursCalendarManager> manager, DateTime? now = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(now ?? _mondayNoonUtc);

        return new DefaultBusinessHoursService(manager.Object, clock.Object);
    }

    private static BusinessHoursCalendar CreateMondayNineToFive(string timeZoneId = "UTC")
    {
        return new BusinessHoursCalendar
        {
            ItemId = "cal1",
            TimeZoneId = timeZoneId,
            Enabled = true,
            WeeklySchedule =
            [
                new BusinessHoursDay { Day = DayOfWeek.Monday, IsOpen = true, OpenMinute = 540, CloseMinute = 1020 },
            ],
        };
    }
}
