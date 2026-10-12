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
}
