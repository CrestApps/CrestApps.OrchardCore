using System.Security.Cryptography;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// The private subscription address of a calendar: a random token in the address, which anyone holding it can read the
/// calendar with. Making a new one stops the old one at once.
/// </summary>
internal static class CalendarFeedAddress
{
    /// <summary>
    /// Makes a new subscription address for a calendar, or removes it. The caller saves the calendar.
    /// </summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="remove">Whether to remove the address instead of making a new one.</param>
    public static void Reset(Calendar calendar, bool remove)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        calendar.FeedToken = remove ? null : Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    }
}
