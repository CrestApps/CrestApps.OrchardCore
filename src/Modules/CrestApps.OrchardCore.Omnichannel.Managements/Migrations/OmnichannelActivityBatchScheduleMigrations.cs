using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Stores every activity load's <see cref="OmnichannelActivityBatch.ScheduleAt"/> in UTC.
/// </summary>
/// <remarks>
/// The load form stored the time as it was typed, in local time, and the loader converted it in the site's time zone
/// when it created the activities. The form now stores UTC, as the single activity form does, so a load saved before is
/// converted the way the loader would have converted it: in the site's time zone.
/// </remarks>
internal sealed class OmnichannelActivityBatchScheduleMigrations : DataMigration
{
    private readonly ICatalog<OmnichannelActivityBatch> _batchCatalog;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityBatchScheduleMigrations"/> class.
    /// </summary>
    /// <param name="batchCatalog">The activity load catalog.</param>
    /// <param name="siteService">The site service, for the site's time zone.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelActivityBatchScheduleMigrations(
        ICatalog<OmnichannelActivityBatch> batchCatalog,
        ISiteService siteService,
        ILogger<OmnichannelActivityBatchScheduleMigrations> logger)
    {
        _batchCatalog = batchCatalog;
        _siteService = siteService;
        _logger = logger;
    }

    /// <summary>
    /// Converts the schedule of every load saved before to UTC.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var timeZone = SiteTimeZone(site.TimeZoneId);
        var converted = 0;

        foreach (var batch in await _batchCatalog.GetAllAsync())
        {
            if (batch.ScheduleAt == DateTime.MinValue)
            {
                continue;
            }

            batch.ScheduleAt = ToUtc(batch.ScheduleAt, timeZone);
            await _batchCatalog.UpdateAsync(batch);
            converted++;
        }

        if (converted > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Stored the schedule of {Count} activity load(s) in UTC, read in the site's time zone '{TimeZone}'.",
                converted,
                timeZone.Id);
        }

        return 1;
    }

    /// <summary>
    /// A local time in <paramref name="timeZone"/>, in UTC.
    /// </summary>
    /// <param name="local">The local time.</param>
    /// <param name="timeZone">The time zone it was given in.</param>
    internal static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // A time the clocks skipped when they went forward did not exist there; it is read as the hour after the change.
        if (timeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
    }

    /// <summary>
    /// The time zone the loader converted the schedule in: the site's, else the server's.
    /// </summary>
    /// <param name="timeZoneId">The site's time zone, or <see langword="null"/>.</param>
    internal static TimeZoneInfo SiteTimeZone(string timeZoneId)
        => !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone)
            ? timeZone
            : TimeZoneInfo.Local;
}
