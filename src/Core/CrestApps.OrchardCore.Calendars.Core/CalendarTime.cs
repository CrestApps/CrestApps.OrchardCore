using NodaTime;
using NodaTime.TimeZones;

namespace CrestApps.OrchardCore.Calendars.Core;

/// <summary>
/// Converts between wall-clock times in an IANA time zone and UTC, the way RFC 5545 asks: a wall-clock time that doesn't exist
/// (skipped by a daylight-saving change) moves forward by the gap, and one that happens twice resolves to the earlier moment.
/// </summary>
public static class CalendarTime
{
    /// <summary>
    /// Gets a time zone by IANA id, or by Windows id, falling back to UTC when the id is empty or unknown.
    /// </summary>
    /// <param name="timeZoneId">The time zone id.</param>
    /// <returns>The time zone.</returns>
    public static DateTimeZone GetZone(string timeZoneId)
    {
        return FindZone(timeZoneId) ?? DateTimeZone.Utc;
    }

    /// <summary>
    /// Gets whether a time zone id is a known IANA or Windows id.
    /// </summary>
    /// <param name="timeZoneId">The time zone id.</param>
    /// <returns><see langword="true"/> when the id is known.</returns>
    public static bool IsValidZone(string timeZoneId)
    {
        return FindZone(timeZoneId) is not null;
    }

    /// <summary>
    /// Gets the IANA id for a time zone id, translating a Windows id, or <see langword="null"/> when it is unknown.
    /// </summary>
    /// <param name="timeZoneId">The time zone id.</param>
    /// <returns>The IANA id.</returns>
    public static string NormalizeZoneId(string timeZoneId)
    {
        return FindZone(timeZoneId)?.Id;
    }

    /// <summary>
    /// Converts a wall-clock time in a time zone to UTC.
    /// </summary>
    /// <param name="local">The wall-clock time; its kind is ignored.</param>
    /// <param name="timeZoneId">The time zone id.</param>
    /// <returns>The UTC time.</returns>
    public static DateTime ToUtc(DateTime local, string timeZoneId)
    {
        return ToUtc(LocalDateTime.FromDateTime(DateTime.SpecifyKind(local, DateTimeKind.Unspecified)), GetZone(timeZoneId));
    }

    /// <summary>
    /// Converts a wall-clock time in a time zone to UTC.
    /// </summary>
    /// <param name="local">The wall-clock time.</param>
    /// <param name="zone">The time zone.</param>
    /// <returns>The UTC time.</returns>
    public static DateTime ToUtc(LocalDateTime local, DateTimeZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return local.InZone(zone, Resolvers.LenientResolver).ToDateTimeUtc();
    }

    /// <summary>
    /// Converts a UTC time to the wall-clock time in a time zone.
    /// </summary>
    /// <param name="utc">The UTC time; an unspecified kind is treated as UTC.</param>
    /// <param name="timeZoneId">The time zone id.</param>
    /// <returns>The wall-clock time, with an unspecified kind.</returns>
    public static DateTime ToLocal(DateTime utc, string timeZoneId)
    {
        var instant = Instant.FromDateTimeUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc));

        return instant.InZone(GetZone(timeZoneId)).LocalDateTime.ToDateTimeUnspecified();
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> to a NodaTime wall-clock time, ignoring its kind.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The wall-clock time.</returns>
    public static LocalDateTime ToLocalDateTime(DateTime value)
    {
        return LocalDateTime.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Unspecified));
    }

    /// <summary>
    /// Truncates a UTC time to whole seconds, the precision iCalendar and the event store keep.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The truncated value.</returns>
    public static DateTime TruncateToSeconds(DateTime value)
    {
        return new DateTime(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), value.Kind);
    }

    private static DateTimeZone FindZone(string timeZoneId)
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

        if (TzdbDateTimeZoneSource.Default.WindowsMapping.PrimaryMapping.TryGetValue(id, out var ianaId))
        {
            return DateTimeZoneProviders.Tzdb.GetZoneOrNull(ianaId);
        }

        return null;
    }
}
