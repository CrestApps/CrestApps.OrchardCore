using CrestApps.OrchardCore.Reports.Models;
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
public sealed class ReportDateRangeFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private const int DefaultRangeDays = 30;

    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDateRangeFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="clock">The clock used to compute the default reporting period.</param>
    /// <param name="localClock">The tenant local clock.</param>
    public ReportDateRangeFilterDisplayDriver(
        IClock clock,
        ILocalClock localClock)
    {
        _clock = clock;
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
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        var model = new ReportDateRangeFilterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var fromUtc = model.From.HasValue
            ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(model.From.Value, DateTimeKind.Unspecified))
            : (DateTime?)null;
        var toUtc = model.To.HasValue
            ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(model.To.Value, DateTimeKind.Unspecified))
            : (DateTime?)null;

        var range = await NormalizeAsync(fromUtc, toUtc, model.Range);

        filter.SetDateRange(range);

        return Edit(filter, context);
    }

    private async Task<ReportDateRange> NormalizeAsync(DateTime? fromUtc, DateTime? toUtc, string key)
    {
        var localNow = await _localClock.ConvertToLocalAsync(_clock.UtcNow);
        var localDate = localNow.Date;
        var defaultFromLocal = DateTime.SpecifyKind(localDate.AddDays(-(DefaultRangeDays - 1)), DateTimeKind.Unspecified);
        var defaultToLocal = DateTime.SpecifyKind(localDate.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);
        var defaultFromUtc = await _localClock.ConvertToUtcAsync(defaultFromLocal);
        var defaultToUtc = await _localClock.ConvertToUtcAsync(defaultToLocal);
        var to = toUtc ?? defaultToUtc;
        var from = fromUtc ?? defaultFromUtc;

        if (from > to)
        {
            (from, to) = (to, from);
        }

        return new ReportDateRange
        {
            FromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc),
            ToUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc),
            Key = key,
        };
    }
}
