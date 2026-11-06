using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Drivers;

/// <summary>
/// Contributes the built-in from/to date-range filter to every report. Because it declares no group, it
/// renders for all reports; report-specific filters restrict themselves to their report's group. The
/// resolved period is stored in the report filter property bag like any other filter, so a report that
/// does not need a date range can be built without one.
/// </summary>
/// <remarks>
/// A report that declares a <see cref="ReportDescriptor.DefaultDateRange"/> starts on it, accepts open-ended ranges
/// ("on or after", "on or before") as they were picked, and offers <em>All Time</em>, which sets neither bound. A
/// report that declares none starts on today, and an empty bound is filled with today's.
/// </remarks>
public sealed class ReportDateRangeFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly IReportManager _reportManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDateRangeFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="clock">The clock used to compute the default reporting period.</param>
    /// <param name="localClock">The tenant local clock.</param>
    /// <param name="reportManager">The report manager, which describes the default range of each report.</param>
    public ReportDateRangeFilterDisplayDriver(
        IClock clock,
        ILocalClock localClock,
        IReportManager reportManager)
    {
        _clock = clock;
        _localClock = localClock;
        _reportManager = reportManager;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        return Initialize<ReportDateRangeFilterViewModel>("ReportDateRangeFilter_Edit", async model =>
        {
            var range = filter.GetDateRange();

            model.From = range.FromUtc.HasValue
                ? (await _localClock.ConvertToLocalAsync(range.FromUtc.Value)).DateTime
                : null;
            model.To = range.ToUtc.HasValue
                ? (await _localClock.ConvertToLocalAsync(range.ToUtc.Value)).DateTime
                : null;
            model.Range = range.Key;
            model.AllowAllTime = GetDefaultRange(filter.ReportName) is not null;
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        var model = new ReportDateRangeFilterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var fromLocal = model.From.HasValue
            ? DateTime.SpecifyKind(model.From.Value, DateTimeKind.Unspecified)
            : (DateTime?)null;
        var toLocal = model.To.HasValue
            ? NormalizeUpperBound(model.To.Value)
            : (DateTime?)null;
        var fromUtc = fromLocal.HasValue
            ? await _localClock.ConvertToUtcAsync(fromLocal.Value)
            : (DateTime?)null;
        var toUtc = toLocal.HasValue
            ? await _localClock.ConvertToUtcAsync(toLocal.Value)
            : (DateTime?)null;

        var range = await NormalizeAsync(filter.ReportName, fromUtc, toUtc, model.Range);

        filter.SetDateRange(range);

        return Edit(filter, context);
    }

    /// <summary>
    /// Resolves the range a report runs on from the bounds that were picked.
    /// </summary>
    /// <param name="reportName">The technical name of the report.</param>
    /// <param name="fromUtc">The lower bound that was picked, in UTC, or <see langword="null"/>.</param>
    /// <param name="toUtc">The upper bound that was picked, in UTC, or <see langword="null"/>.</param>
    /// <param name="key">The key of the date range choice that was picked.</param>
    /// <returns>The range the report runs on.</returns>
    internal async Task<ReportDateRange> NormalizeAsync(string reportName, DateTime? fromUtc, DateTime? toUtc, string key)
    {
        var defaultRange = GetDefaultRange(reportName);

        if (defaultRange is null)
        {
            // A report that declares no default starts on today, and runs on a full range.
            var (todayFromUtc, todayToUtc) = await ResolveUtcAsync(ReportDefaultDateRange.Today);

            fromUtc ??= todayFromUtc;
            toUtc ??= todayToUtc;
        }
        else if (string.Equals(key, ReportDefaultDateRange.AllTimeKey, StringComparison.Ordinal))
        {
            fromUtc = null;
            toUtc = null;
        }
        else if (fromUtc is null && toUtc is null)
        {
            (fromUtc, toUtc) = await ResolveUtcAsync(defaultRange);
            key = defaultRange.Key;
        }

        // A single bound is "on or after" or "on or before", so only a full range can be inverted.
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        return new ReportDateRange
        {
            FromUtc = fromUtc.HasValue ? DateTime.SpecifyKind(fromUtc.Value, DateTimeKind.Utc) : null,
            ToUtc = toUtc.HasValue ? DateTime.SpecifyKind(toUtc.Value, DateTimeKind.Utc) : null,
            Key = key,
        };
    }

    private ReportDefaultDateRange GetDefaultRange(string reportName)
        => _reportManager.GetDescriptor(reportName)?.DefaultDateRange;

    private async Task<(DateTime? FromUtc, DateTime? ToUtc)> ResolveUtcAsync(ReportDefaultDateRange range)
    {
        var localNow = (await _localClock.ConvertToLocalAsync(_clock.UtcNow)).DateTime;
        var (fromLocal, toLocal) = range.Resolve(localNow);

        return (
            fromLocal.HasValue ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(fromLocal.Value, DateTimeKind.Unspecified)) : null,
            toLocal.HasValue ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(toLocal.Value, DateTimeKind.Unspecified)) : null);
    }

    private static DateTime NormalizeUpperBound(DateTime value)
    {
        var localValue = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        if (localValue.Hour == 23 &&
            localValue.Minute == 59 &&
            localValue.Second == 0 &&
            localValue.Millisecond == 0)
        {
            return localValue.AddSeconds(59);
        }

        return localValue;
    }
}
