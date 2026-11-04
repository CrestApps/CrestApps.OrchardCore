using NodaTime;
using NodaTime.TimeZones;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Reads time zone ids the way every calendar, team and worker does: an IANA id, or a Windows id translated to its IANA
/// id. Use it instead of writing another id check.
/// </summary>
public static class CalendarTimeZones
{
    /// <summary>
    /// Gets the time zone for an IANA or Windows id, or <see langword="null"/> when the id is empty or unknown.
    /// </summary>
    /// <param name="timeZoneId">The time zone id.</param>
    /// <returns>The time zone, or <see langword="null"/>.</returns>
    public static DateTimeZone Find(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        var id = timeZoneId.Trim();
        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(id);

        if (zone is not null)
        {
            return zone;
        }

        return TzdbDateTimeZoneSource.Default.WindowsMapping.PrimaryMapping.TryGetValue(id, out var ianaId)
            ? DateTimeZoneProviders.Tzdb.GetZoneOrNull(ianaId)
            : null;
    }

    /// <summary>
    /// Gets the IANA id for a time zone id, translating a Windows id, or <see langword="null"/> when it is empty or
    /// unknown.
    /// </summary>
    /// <param name="timeZoneId">The time zone id.</param>
    /// <returns>The IANA id, or <see langword="null"/>.</returns>
    public static string Normalize(string timeZoneId)
    {
        return Find(timeZoneId)?.Id;
    }

    /// <summary>
    /// Gets whether a time zone id is a known IANA or Windows id.
    /// </summary>
    /// <param name="timeZoneId">The time zone id.</param>
    /// <returns><see langword="true"/> when the id is known.</returns>
    public static bool IsValid(string timeZoneId)
    {
        return Find(timeZoneId) is not null;
    }
}
