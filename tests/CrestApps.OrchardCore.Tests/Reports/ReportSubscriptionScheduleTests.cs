using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using CrestApps.OrchardCore.Reports.Subscriptions.Services;

namespace CrestApps.OrchardCore.Tests.Reports;

/// <summary>
/// A subscribed report goes out at its hour in the site's time zone, daily, on its weekday or on its day of the month,
/// and covers the day, week or month before.
/// </summary>
public sealed class ReportSubscriptionScheduleTests
{
    private static readonly TimeZoneInfo _newYork = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");

    [Fact]
    public void AWeeklyReport_GoesOutOnItsDayAndHour_InTheSitesTimeZone()
    {
        var subscription = new ReportSubscription { Frequency = ReportSubscriptionFrequency.Weekly, DayOfWeek = DayOfWeek.Monday, Hour = 7 };

        // Tuesday 20 October 2026, 9:00 UTC (5:00 in New York): next Monday, 26 October, 7:00 in New York.
        Assert.Equal(new DateTime(2026, 10, 26, 11, 0, 0, DateTimeKind.Utc), ReportSubscriptionSchedule.GetNextRunUtc(subscription, new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc), _newYork));

        // Run on that Monday at 7:00 exactly, the next is a week on, after the clocks go back on 1 November: 12:00 UTC.
        Assert.Equal(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc), ReportSubscriptionSchedule.GetNextRunUtc(subscription, new DateTime(2026, 10, 26, 11, 0, 0, DateTimeKind.Utc), _newYork));
    }

    [Fact]
    public void ADailyOrMonthlyReport_GoesOutTheNextTimeItsHourOrDayComes()
    {
        var daily = new ReportSubscription { Frequency = ReportSubscriptionFrequency.Daily, Hour = 6 };
        var monthly = new ReportSubscription { Frequency = ReportSubscriptionFrequency.Monthly, DayOfMonth = 1, Hour = 8 };
        var now = new DateTime(2026, 10, 20, 5, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 10, 20, 6, 0, 0, DateTimeKind.Utc), ReportSubscriptionSchedule.GetNextRunUtc(daily, now, TimeZoneInfo.Utc));
        Assert.Equal(new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc), ReportSubscriptionSchedule.GetNextRunUtc(monthly, now, TimeZoneInfo.Utc));
    }

    [Theory]
    [InlineData(ReportSubscriptionFrequency.Daily, "2026-10-19", "2026-10-20")]
    [InlineData(ReportSubscriptionFrequency.Weekly, "2026-10-13", "2026-10-20")]
    [InlineData(ReportSubscriptionFrequency.Monthly, "2026-09-01", "2026-10-01")]
    public void AReport_CoversThePeriodBeforeItsRun(ReportSubscriptionFrequency frequency, string from, string to)
    {
        var (fromUtc, toUtc) = ReportSubscriptionSchedule.GetPeriod(new ReportSubscription { Frequency = frequency }, new DateTime(2026, 10, 20, 7, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Utc);

        Assert.Equal((DateTime.Parse(from, System.Globalization.CultureInfo.InvariantCulture), DateTime.Parse(to, System.Globalization.CultureInfo.InvariantCulture)), (fromUtc, toUtc));
    }
}
