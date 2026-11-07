using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// Weekly hours that open or close at a wall-clock time skipped by a daylight-saving change move forward by the gap
/// instead of throwing.
/// </summary>
public sealed class SlaClockDaylightSavingTests
{
    // Sunday 14 March 2027: clocks in New York jump from 02:00 to 03:00.
    private static readonly DateOnly _springForward = new(2027, 3, 14);

    [Fact]
    public void OpenWeekly_WhenTheOpeningTimeIsSkippedBySpringForward_OpensAtTheEndOfTheGap()
    {
        // Arrange
        var weekly = new WeeklyHours
        {
            Days = [DayOfWeek.Sunday],
            Opens = new TimeOnly(2, 30),
            Closes = new TimeOnly(6, 0),
        };
        var fromUtc = new DateTime(2027, 3, 14, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var open = SlaClock.OpenWeekly(weekly, "America/New_York", fromUtc, fromUtc.AddDays(1)).ToList();

        // Assert
        var window = Assert.Single(open);

        // 02:30 doesn't exist; it moves forward by the gap to 03:30 EDT, 07:30 UTC. 06:00 EDT is 10:00 UTC.
        Assert.Equal(new DateTime(2027, 3, 14, 7, 30, 0, DateTimeKind.Utc), window.StartUtc);
        Assert.Equal(new DateTime(2027, 3, 14, 10, 0, 0, DateTimeKind.Utc), window.EndUtc);
        Assert.Equal(_springForward, DateOnly.FromDateTime(window.StartUtc));
    }

    [Fact]
    public void OpenWeekly_WithAWindowsTimeZoneId_ReadsItAsItsIanaZone()
    {
        // Arrange
        var weekly = new WeeklyHours { Days = [DayOfWeek.Monday], Opens = new TimeOnly(9, 0), Closes = new TimeOnly(17, 0) };
        var fromUtc = new DateTime(2027, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var window = Assert.Single(SlaClock.OpenWeekly(weekly, "Eastern Standard Time", fromUtc, fromUtc.AddDays(1)));

        // Assert
        Assert.Equal(new DateTime(2027, 3, 15, 13, 0, 0, DateTimeKind.Utc), window.StartUtc);
        Assert.Equal(new DateTime(2027, 3, 15, 21, 0, 0, DateTimeKind.Utc), window.EndUtc);
    }
}
