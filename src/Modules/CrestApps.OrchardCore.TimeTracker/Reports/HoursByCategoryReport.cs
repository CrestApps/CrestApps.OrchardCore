using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeTracker.Reports;

/// <summary>
/// The hours of each category: a row for each person, or each person and day, and a column for each enabled
/// category, for the time tracked in the period. It starts on this week, Monday to Sunday, and reads the bounds of the
/// period in the site's time zone, as the time tracker counts its days.
/// </summary>
public sealed class HoursByCategoryReport : IReport
{
    /// <summary>
    /// The key of the user name search in the report filter.
    /// </summary>
    public const string SearchFilterKey = "TimeTrackerSearch";

    /// <summary>
    /// The key of the "a row per day" choice in the report filter.
    /// </summary>
    public const string ByDayFilterKey = "TimeTrackerByDay";

    private readonly ITimeTrackerService _tracker;
    private readonly TimeCategoryStore _categories;
    private readonly TimeTrackerTimeZone _timeZone;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="HoursByCategoryReport"/> class.
    /// </summary>
    /// <param name="tracker">The time tracker service, which totals the time.</param>
    /// <param name="categories">The category store, for the report's columns.</param>
    /// <param name="timeZone">The time zone days are counted in.</param>
    /// <param name="localClock">The local clock the date range was picked in.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public HoursByCategoryReport(
        ITimeTrackerService tracker,
        TimeCategoryStore categories,
        TimeTrackerTimeZone timeZone,
        ILocalClock localClock,
        IStringLocalizer<HoursByCategoryReport> stringLocalizer)
    {
        _tracker = tracker;
        _categories = categories;
        _timeZone = timeZone;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the technical name of the report.
    /// </summary>
    public static string Name => "time-tracker-hours-by-category";

    /// <summary>
    /// Gets the display name of the report.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<HoursByCategoryReport>("Hours by Category");

    /// <summary>
    /// Gets the description of the report.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<HoursByCategoryReport>("The tracked hours of each person, or each person and day, in each time category.");

    /// <summary>
    /// Gets the category of the report.
    /// </summary>
    public static string Category => ReportsConstants.Categories.WorkforcePayroll;

    /// <summary>
    /// Gets the permission that runs and exports the report.
    /// </summary>
    public static Permission Permission => TimeTrackerPermissions.ViewTimeEntries;

    /// <summary>
    /// Gets the date range the report starts on: this week, Monday to Sunday, as the time report did.
    /// </summary>
    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.ThisWeek(DayOfWeek.Monday);

    /// <inheritdoc/>
    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var range = context.Filter.GetDateRange();
        var byDay = context.Filter.GetOrDefault<bool>(ByDayFilterKey);
        var search = context.Filter.GetOrDefault<string>(SearchFilterKey)?.Trim();
        var zone = await _timeZone.GetAsync();
        var fromUtc = await InSiteTimeZoneAsync(range.FromUtc, zone);

        // The time tracker reads a period up to an exclusive end, and the date range ends on the last second of its
        // last day.
        DateTime? toUtc = await InSiteTimeZoneAsync(range.ToUtc, zone) is { } end
            ? new DateTime(end.Ticks - (end.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc).AddSeconds(1)
            : null;

        var rows = (await _tracker.GetTotalsAsync(new TimeEntryQuery { FromUtc = fromUtc, ToUtc = toUtc }, zone, byDay, cancellationToken))
            .Where(row => string.IsNullOrEmpty(search) || (row.UserName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rows.Count == 0)
        {
            if (context.Mode == ReportRunMode.Export)
            {
                return new ReportDocument();
            }

            var empty = ReportSection.ForTable(S["Hours by category ({0})", zone].Value, [], []);
            empty.EmptyMessage = S["No time in these days."].Value;

            return new ReportDocument().Add(empty);
        }

        var categories = (await _categories.GetAllAsync(cancellationToken))
            .Where(category => category.Enabled)
            .OrderBy(category => category.Position)
            .ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var columns = new List<ReportColumn> { new(S["Person"].Value) };

        if (byDay)
        {
            columns.Add(new ReportColumn(S["Day"].Value));
        }

        columns.AddRange(categories.Select(category => new ReportColumn(category.Name, ReportColumnAlign.End)));
        columns.Add(new ReportColumn(S["Total"].Value, ReportColumnAlign.End));

        var tableRows = rows.Select(row =>
        {
            var cells = new List<string> { row.UserName };

            if (byDay)
            {
                // On screen the day reads as the time report showed it; an export keeps a date that sorts and parses.
                cells.Add(context.Mode == ReportRunMode.Export
                    ? row.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : row.Date?.ToString("ddd d MMM", CultureInfo.CurrentCulture));
            }

            cells.AddRange(categories.Select(category => TimeTrackerTimeZone.Hours(row.MinutesByCategory.GetValueOrDefault(category.ItemId))));
            cells.Add(TimeTrackerTimeZone.Hours(row.TotalMinutes));

            return new ReportRow(cells);
        }).ToList();

        var total = new List<string> { S["Everyone"].Value };

        if (byDay)
        {
            total.Add(string.Empty);
        }

        total.AddRange(categories.Select(category => TimeTrackerTimeZone.Hours(rows.Sum(row => row.MinutesByCategory.GetValueOrDefault(category.ItemId)))));
        total.Add(TimeTrackerTimeZone.Hours(rows.Sum(row => row.TotalMinutes)));
        tableRows.Add(new ReportRow(total, ReportRowKind.GrandTotal));

        var section = ReportSection.ForTable(S["Hours by category ({0})", zone].Value, columns, tableRows);
        section.Description = S["A stage counts toward each of its categories; a category's factor counts its minutes more or less. Hours are hours:minutes."].Value;

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["People"].Value, ReportFormat.Number(rows.Select(row => row.UserId).Distinct(StringComparer.Ordinal).Count())),
                new ReportMetric(S["Total hours"].Value, TimeTrackerTimeZone.Hours(rows.Sum(row => row.TotalMinutes))),
            ]))
            .Add(section);
    }

    // The date range is picked in the time zone of the person; the time tracker counts its days in the site's time zone,
    // as its own pages do, so the same wall-clock bound is read in the site's time zone.
    private async Task<DateTime?> InSiteTimeZoneAsync(DateTime? utc, string zone)
    {
        if (utc is not { } value)
        {
            return null;
        }

        var wallClock = (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(value, DateTimeKind.Utc))).DateTime;

        return TimeTrackerTimeZone.ToUtc(DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified), zone);
    }
}
