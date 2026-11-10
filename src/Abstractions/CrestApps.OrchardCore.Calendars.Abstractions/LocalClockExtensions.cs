using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Converts the times people enter in the site's time zone to UTC without failing on a daylight-saving change.
/// </summary>
public static class LocalClockExtensions
{
    /// <summary>
    /// Converts a wall-clock time in the local time zone (the user's or the site's) to UTC. Unlike
    /// <see cref="ILocalClock.ConvertToUtcAsync(DateTime)"/>, a time a daylight-saving change skips moves forward by the
    /// gap, and one that happens twice is the earlier of the two, so a form never fails on it.
    /// </summary>
    /// <param name="localClock">The local clock.</param>
    /// <param name="local">The wall-clock time; its kind is ignored.</param>
    /// <returns>The UTC time.</returns>
    public static async Task<DateTime> ToUtcLenientAsync(this ILocalClock localClock, DateTime local)
    {
        ArgumentNullException.ThrowIfNull(localClock);

        var timeZone = await localClock.GetLocalTimeZoneAsync();

        return CalendarTime.ToUtc(local, timeZone?.TimeZoneId);
    }
}
