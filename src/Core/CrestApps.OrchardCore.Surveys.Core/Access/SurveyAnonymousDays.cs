using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// The one definition of the day an anonymous response's times are shown by: the day in the site's time zone (the
/// site's setting, else the server's), never the viewer's. A viewer's own time zone (OrchardCore.Users.TimeZone) would
/// let them step it, hour by hour, and watch where a response's day flips, which tells its time to the hour. Data sets,
/// the API, admin pages, reports, exports, and webhook and workflow payloads all read their days here. A day shown as a
/// date is a pure date, so it is rendered as preformatted text, never converted again.
/// </summary>
public sealed class SurveyAnonymousDays
{
    private readonly IServiceProvider _serviceProvider;
    private TimeZoneInfo _timeZone;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnonymousDays"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, for the site settings and the clock when they are there.</param>
    public SurveyAnonymousDays(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the site's time zone: the site's setting, else the server's time zone, else UTC. Read once per scope.
    /// </summary>
    /// <returns>The time zone.</returns>
    public async Task<TimeZoneInfo> TimeZoneAsync()
    {
        if (_timeZone is not null)
        {
            return _timeZone;
        }

        var timeZoneId = _serviceProvider.GetService<ISiteService>() is { } siteService ? (await siteService.GetSiteSettingsAsync())?.TimeZoneId : null;

        if (string.IsNullOrEmpty(timeZoneId))
        {
            timeZoneId = _serviceProvider.GetService<IClock>()?.GetSystemTimeZone()?.TimeZoneId;
        }

        return _timeZone = Find(timeZoneId);
    }

    /// <summary>
    /// Gets the start of the day a time falls on in the site's time zone, as UTC: an anonymous response's time as a
    /// machine output (data set, API, export, payload) shows it.
    /// </summary>
    /// <param name="utc">The time, in UTC, or <see langword="null"/>.</param>
    /// <returns>The start of its day, in UTC, or <see langword="null"/>.</returns>
    public async Task<DateTime?> StartOfDayUtcAsync(DateTime? utc)
        => utc is { } value ? SurveyAnonymity.StartOfDayUtc(value, await TimeZoneAsync()) : null;

    /// <summary>
    /// Gets the day a time falls on in the site's time zone.
    /// </summary>
    /// <param name="utc">The time, in UTC, or <see langword="null"/>.</param>
    /// <returns>The day, or <see langword="null"/>.</returns>
    public async Task<DateOnly?> DayAsync(DateTime? utc)
        => utc is { } value && value != DateTime.MinValue ? SurveyAnonymity.Day(value, await TimeZoneAsync()) : null;

    /// <summary>
    /// Formats the day a time falls on in the site's time zone as a short date in the current culture, for a page or a
    /// report: preformatted, so nothing converts it again in the viewer's time zone.
    /// </summary>
    /// <param name="utc">The time, in UTC, or <see langword="null"/>.</param>
    /// <returns>The date as text, or an empty string.</returns>
    public async Task<string> TextAsync(DateTime? utc)
        => await DayAsync(utc) is { } day ? day.ToString("d", CultureInfo.CurrentCulture) : string.Empty;

    /// <summary>
    /// Finds a time zone by id.
    /// </summary>
    /// <param name="timeZoneId">The id, or <see langword="null"/>.</param>
    /// <returns>The time zone; UTC when the id is empty or unknown.</returns>
    public static TimeZoneInfo Find(string timeZoneId)
        => !string.IsNullOrEmpty(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) ? zone : TimeZoneInfo.Utc;
}
