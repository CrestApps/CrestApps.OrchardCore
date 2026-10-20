using CrestApps.OrchardCore.Reports.Subscriptions.Models;

namespace CrestApps.OrchardCore.Reports.Subscriptions.Services;

/// <summary>
/// When a subscription's report goes out, and the period it covers, in the site's time zone.
/// </summary>
public static class ReportSubscriptionSchedule
{
    /// <summary>
    /// Returns the next time after <paramref name="afterUtc"/> the report goes out.
    /// </summary>
    public static DateTime GetNextRunUtc(ReportSubscription subscription, DateTime afterUtc, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        timeZone ??= TimeZoneInfo.Utc;

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc), timeZone);
        var hour = Math.Clamp(subscription.Hour, 0, 23);
        var candidate = new DateTime(local.Year, local.Month, local.Day, hour, 0, 0, DateTimeKind.Unspecified);

        switch (subscription.Frequency)
        {
            case ReportSubscriptionFrequency.Daily:
                if (candidate <= local)
                {
                    candidate = candidate.AddDays(1);
                }

                break;

            case ReportSubscriptionFrequency.Weekly:
                candidate = candidate.AddDays(((int)subscription.DayOfWeek - (int)candidate.DayOfWeek + 7) % 7);

                if (candidate <= local)
                {
                    candidate = candidate.AddDays(7);
                }

                break;

            default:
                var day = Math.Clamp(subscription.DayOfMonth, 1, 28);

                candidate = new DateTime(local.Year, local.Month, day, hour, 0, 0, DateTimeKind.Unspecified);

                if (candidate <= local)
                {
                    candidate = candidate.AddMonths(1);
                }

                break;
        }

        return ToUtc(candidate, timeZone);
    }

    /// <summary>
    /// Returns the period a run at <paramref name="runUtc"/> covers: the day, seven days or calendar month before it,
    /// from midnight to midnight in the site's time zone.
    /// </summary>
    public static (DateTime FromUtc, DateTime ToUtc) GetPeriod(ReportSubscription subscription, DateTime runUtc, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        timeZone ??= TimeZoneInfo.Utc;

        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(runUtc, DateTimeKind.Utc), timeZone).Date;

        var (from, to) = subscription.Frequency switch
        {
            ReportSubscriptionFrequency.Daily => (today.AddDays(-1), today),
            ReportSubscriptionFrequency.Weekly => (today.AddDays(-7), today),
            _ => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1)),
        };

        return (ToUtc(from, timeZone), ToUtc(to, timeZone));
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // A time a clock change skips is taken an hour later.
        if (timeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
    }
}
