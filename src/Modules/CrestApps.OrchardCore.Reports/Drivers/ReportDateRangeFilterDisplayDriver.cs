using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Drivers;

/// <summary>
/// Contributes the built-in from/to date-range filter to every report. Because it declares no group, it
/// renders for all reports; report-specific filters restrict themselves to their report's group.
/// </summary>
public sealed class ReportDateRangeFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDateRangeFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="localClock">The tenant local clock.</param>
    public ReportDateRangeFilterDisplayDriver(ILocalClock localClock)
    {
        _localClock = localClock;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        return Initialize<ReportDateRangeFilterViewModel>("ReportDateRangeFilter_Edit", async model =>
        {
            model.From = filter.FromUtc.HasValue
                ? (await _localClock.ConvertToLocalAsync(filter.FromUtc.Value)).DateTime
                : null;
            model.To = filter.ToUtc.HasValue
                ? (await _localClock.ConvertToLocalAsync(filter.ToUtc.Value)).DateTime
                : null;
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        var model = new ReportDateRangeFilterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.FromUtc = model.From.HasValue
            ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(model.From.Value, DateTimeKind.Unspecified))
            : null;
        filter.ToUtc = model.To.HasValue
            ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(model.To.Value, DateTimeKind.Unspecified))
            : null;

        return Edit(filter, context);
    }
}
