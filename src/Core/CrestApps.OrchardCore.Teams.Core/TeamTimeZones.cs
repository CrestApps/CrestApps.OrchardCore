using NodaTime;
using NodaTime.TimeZones;

namespace CrestApps.OrchardCore.Teams.Core;

/// <summary>
/// Checks and normalizes the time zone of a team: an IANA id, or a Windows id that is translated to its IANA id.
/// </summary>
public static class TeamTimeZones
{
    /// <summary>
    /// Gets the IANA id for a time zone id, translating a Windows id, or <see langword="null"/> when it is empty or
    /// unknown.
    /// </summary>
    /// <param name="timeZoneId">The time zone id.</param>
    /// <returns>The IANA id, or <see langword="null"/>.</returns>
    public static string Normalize(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        var id = timeZoneId.Trim();

        if (DateTimeZoneProviders.Tzdb.GetZoneOrNull(id) is { } zone)
        {
            return zone.Id;
        }

        return TzdbDateTimeZoneSource.Default.WindowsMapping.PrimaryMapping.TryGetValue(id, out var ianaId)
            ? DateTimeZoneProviders.Tzdb.GetZoneOrNull(ianaId)?.Id
            : null;
    }

    /// <summary>
    /// Gets whether a time zone id is a known IANA or Windows id.
    /// </summary>
    /// <param name="timeZoneId">The time zone id.</param>
    /// <returns><see langword="true"/> when the id is known.</returns>
    public static bool IsValid(string timeZoneId)
    {
        return Normalize(timeZoneId) is not null;
    }
}
