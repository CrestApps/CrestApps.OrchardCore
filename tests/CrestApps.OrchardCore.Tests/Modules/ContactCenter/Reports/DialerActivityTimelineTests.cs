using CrestApps.OrchardCore.ContactCenter.Reports.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// The dialer activity report counts calls and routing decisions per hour, by the hour they happened in the tenant's
/// time zone, and counts a folded decision event as the decisions it stands for.
/// </summary>
public sealed class DialerActivityTimelineTests
{
    private static readonly DateTime _from = new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Count_PutsEachOccurrenceInItsHour_AndWeighsItByItsCount()
    {
        // Arrange
        var timeline = DialerActivityTimeline.Create(_from, _from.AddHours(2).AddMinutes(30), ReportTimeZone.Utc);

        // Act
        var counts = timeline.Count(
        [
            (_from.AddMinutes(5), 1),
            (_from.AddMinutes(59), 1),
            (_from.AddHours(1).AddMinutes(10), 5),
            (_from.AddHours(2).AddMinutes(1), 1),
            (_from.AddDays(1), 1),
        ]);

        // Assert
        Assert.True(timeline.Hourly);
        Assert.Equal(["Oct 17 08:00", "Oct 17 09:00", "Oct 17 10:00"], timeline.Labels);
        Assert.Equal([2d, 5d, 1d], counts);
    }

    [Fact]
    public void APeriodLongerThanAMonth_IsCountedByDay()
    {
        // Act
        var timeline = DialerActivityTimeline.Create(_from, _from.AddDays(DialerActivityTimeline.MaxHourlyDays + 9), ReportTimeZone.Utc);

        // Assert
        Assert.False(timeline.Hourly);
        Assert.Equal(DialerActivityTimeline.MaxHourlyDays + 10, timeline.BucketStarts.Count);
    }

    [Fact]
    public void Hours_AreTheTenantsLocalHours()
    {
        // Arrange: 08:00 UTC is 10:00 in a zone two hours ahead in October.
        var timeline = DialerActivityTimeline.Create(_from, _from.AddMinutes(30), ReportTimeZone.FromId("Europe/Berlin"));

        // Act
        var counts = timeline.Count([(_from.AddMinutes(10), 1)]);

        // Assert
        Assert.Equal(["Oct 17 10:00"], timeline.Labels);
        Assert.Equal([1d], counts);
    }
}
