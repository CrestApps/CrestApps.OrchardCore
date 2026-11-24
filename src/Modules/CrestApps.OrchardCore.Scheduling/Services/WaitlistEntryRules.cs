using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// The rules of adding a customer to the waitlist by hand. The Waitlist page and the scheduling AI tools both use them.
/// </summary>
internal static class WaitlistEntryRules
{
    /// <summary>
    /// Builds a waitlist entry for whole days, from the start of the first day to the end of the last day in the site's time zone.
    /// </summary>
    /// <param name="appointmentTypeId">The appointment type, already known to exist.</param>
    /// <param name="from">The first day the customer can come, as yyyy-MM-dd.</param>
    /// <param name="to">The last day the customer can come, as yyyy-MM-dd.</param>
    /// <param name="customerName">The customer's name.</param>
    /// <param name="customerEmail">The customer's email.</param>
    /// <param name="customerPhone">The customer's phone number.</param>
    /// <param name="timeZoneId">The site's time zone.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The entry, or <see langword="null"/> when the name is missing or the days can't be read or are the wrong way round.</returns>
    public static WaitlistEntry NewEntry(string appointmentTypeId, string from, string to, string customerName, string customerEmail, string customerPhone, string timeZoneId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(customerName) ||
            !DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) ||
            !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last) || last < first)
        {
            return null;
        }

        return new WaitlistEntry
        {
            AppointmentTypeId = appointmentTypeId,
            FromUtc = CalendarTime.ToUtc(first, timeZoneId),
            ToUtc = CalendarTime.ToUtc(last.AddDays(1), timeZoneId),
            CustomerName = customerName.Trim(),
            CustomerEmail = customerEmail?.Trim(),
            CustomerPhone = customerPhone?.Trim(),
            CreatedUtc = nowUtc,
        };
    }
}
