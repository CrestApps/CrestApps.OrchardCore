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
    private readonly ReportDateRangeResolver _resolver;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDateRangeFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="resolver">The resolver of the period a report runs on.</param>
    /// <param name="localClock">The tenant local clock.</param>
    public ReportDateRangeFilterDisplayDriver(
        ReportDateRangeResolver resolver,
        ILocalClock localClock)
    {
        _resolver = resolver;
        _localClock = localClock;
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
            model.AllowAllTime = _resolver.AllowsAllTime(filter.ReportName);
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
    internal Task<ReportDateRange> NormalizeAsync(string reportName, DateTime? fromUtc, DateTime? toUtc, string key)
        => _resolver.NormalizeAsync(reportName, fromUtc, toUtc, key);

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
