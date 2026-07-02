using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Reports.Drivers;

/// <summary>
/// Contributes the built-in from/to date-range filter to every report. Because it declares no group, it
/// renders for all reports; report-specific filters restrict themselves to their report's group.
/// </summary>
public sealed class ReportDateRangeFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        return Initialize<ReportDateRangeFilterViewModel>("ReportDateRangeFilter_Edit", model =>
        {
            model.From = filter.FromUtc;
            model.To = filter.ToUtc;
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        var model = new ReportDateRangeFilterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.FromUtc = model.From;
        filter.ToUtc = model.To;

        return Edit(filter, context);
    }
}
