using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.TimeTracker.Reports;
using CrestApps.OrchardCore.TimeTracker.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.TimeTracker.Drivers;

/// <summary>
/// The user name search and the "a row per day" choice of the hours by category report.
/// </summary>
public sealed class HoursByCategoryReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (filter.ReportName != HoursByCategoryReport.Name)
        {
            return null;
        }

        return Initialize<HoursByCategoryReportFilterViewModel>("HoursByCategoryReportFilter_Edit", model =>
        {
            model.Q = filter.GetOrDefault<string>(HoursByCategoryReport.SearchFilterKey);
            model.ByDay = filter.GetOrDefault<bool>(HoursByCategoryReport.ByDayFilterKey);
        }).Location("Content:2");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (filter.ReportName != HoursByCategoryReport.Name)
        {
            return null;
        }

        var model = new HoursByCategoryReportFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.Set(HoursByCategoryReport.SearchFilterKey, model.Q?.Trim());
        filter.Set<bool?>(HoursByCategoryReport.ByDayFilterKey, model.ByDay ? true : null);

        return Edit(filter, context);
    }
}
