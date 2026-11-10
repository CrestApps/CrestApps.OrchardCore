using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Dates printed on invoices and receipts. They are stored in UTC, but a customer reads them as the site's own
/// date and time.
/// </summary>
internal static class DocumentDates
{
    /// <summary>
    /// Converts a stored UTC time into the site's time zone. Without a clock it stays in UTC.
    /// </summary>
    /// <param name="localClock">The site's clock, or <see langword="null"/>.</param>
    /// <param name="utc">The stored UTC time.</param>
    public static async Task<DateTime> ToSiteTimeAsync(ILocalClock localClock, DateTime utc)
    {
        if (localClock is null)
        {
            return utc;
        }

        var local = await localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)));

        return local.DateTime;
    }

    /// <summary>
    /// Returns the site's date a payment is due. Some due dates are calendar dates kept at midnight UTC, such as an
    /// installment's, and stay that date; any other due time is read in the site's time zone.
    /// </summary>
    /// <param name="localClock">The site's clock, or <see langword="null"/>.</param>
    /// <param name="dueUtc">The stored due time.</param>
    public static async Task<DateTime> ToSiteDueDateAsync(ILocalClock localClock, DateTime dueUtc)
    {
        if (dueUtc.TimeOfDay == TimeSpan.Zero)
        {
            return dueUtc.Date;
        }

        return (await ToSiteTimeAsync(localClock, dueUtc)).Date;
    }
}
