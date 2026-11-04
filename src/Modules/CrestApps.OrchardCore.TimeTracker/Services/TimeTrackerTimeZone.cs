using System.Globalization;
using NodaTime;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.TimeTracker.Services;

/// <summary>
/// The time zone time is shown and counted in by day: the site's.
/// </summary>
public sealed class TimeTrackerTimeZone
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeTrackerTimeZone"/> class.
    /// </summary>
    public TimeTrackerTimeZone(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <summary>
    /// Gets the time zone's id.
    /// </summary>
    /// <returns>The id.</returns>
    public async Task<string> GetAsync()
    {
        var zone = (await _siteService.GetSiteSettingsAsync()).TimeZoneId;

        return DateTimeZoneProviders.Tzdb.GetZoneOrNull(zone ?? string.Empty) is null ? "UTC" : zone;
    }

    /// <summary>
    /// Turns a UTC time into the zone's wall-clock time.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <param name="zone">The zone.</param>
    /// <returns>The wall-clock time.</returns>
    public static DateTime ToLocal(DateTime utc, string zone)
        => Instant.FromDateTimeUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).InZone(DateTimeZoneProviders.Tzdb[zone]).LocalDateTime.ToDateTimeUnspecified();

    /// <summary>
    /// Turns the zone's wall-clock time into UTC; a time skipped by a clock change moves later.
    /// </summary>
    /// <param name="local">The wall-clock time.</param>
    /// <param name="zone">The zone.</param>
    /// <returns>The time, in UTC.</returns>
    public static DateTime ToUtc(DateTime local, string zone)
        => LocalDateTime.FromDateTime(local).InZoneLeniently(DateTimeZoneProviders.Tzdb[zone]).ToDateTimeUtc();

    /// <summary>
    /// Formats a UTC time in the zone.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <param name="zone">The zone.</param>
    /// <param name="format">The format.</param>
    /// <returns>The text.</returns>
    public static string Format(DateTime? utc, string zone, string format = "ddd d MMM, HH:mm")
        => utc is null ? null : ToLocal(utc.Value, zone).ToString(format, CultureInfo.CurrentCulture);

    /// <summary>
    /// Formats minutes as hours and minutes, such as 7:30.
    /// </summary>
    /// <param name="minutes">The minutes.</param>
    /// <returns>The text.</returns>
    public static string Hours(decimal minutes)
    {
        var whole = (int)Math.Round(minutes);

        return string.Create(CultureInfo.CurrentCulture, $"{whole / 60}:{Math.Abs(whole % 60):00}");
    }
}
