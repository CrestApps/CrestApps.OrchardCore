using NodaTime;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Puts a time zone id in its canonical IANA form, the form contacts and filters store.
/// </summary>
internal static class TimeZoneIds
{
    /// <summary>
    /// Returns the canonical IANA id of a time zone, or <see langword="null"/> when the id is empty or unknown.
    /// </summary>
    /// <param name="timeZoneId">The id to normalize.</param>
    public static string Normalize(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        return DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId.Trim())?.Id;
    }
}
