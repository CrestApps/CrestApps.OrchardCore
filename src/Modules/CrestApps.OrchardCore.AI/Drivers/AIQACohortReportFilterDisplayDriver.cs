using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Reports;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// The QA cohort filter of the QA dashboard report. A cohort's list entry links to the report with its identifier
/// in the <c>QACohortId</c> query value.
/// </summary>
public sealed class AIQACohortReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private readonly IAIQACohortStore _cohortStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIQACohortReportFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="cohortStore">The QA cohort store, for the cohorts to choose from.</param>
    public AIQACohortReportFilterDisplayDriver(IAIQACohortStore cohortStore)
    {
        _cohortStore = cohortStore;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (filter.ReportName != AIQualityReport.Name)
        {
            return null;
        }

        return Initialize<AIQACohortReportFilterViewModel>("AIQACohortReportFilter_Edit", async model =>
        {
            model.QACohortId = filter.GetOrDefault<string>(AIQualityReport.CohortIdFilterKey);
            model.Cohorts = (await _cohortStore.GetAllAsync())
                .OrderBy(cohort => cohort.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(cohort => new SelectListItem(cohort.Name, cohort.ItemId, cohort.ItemId == model.QACohortId))
                .ToList();
        }).Location("Content:2");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (filter.ReportName != AIQualityReport.Name)
        {
            return null;
        }

        var model = new AIQACohortReportFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.Set(AIQualityReport.CohortIdFilterKey, model.QACohortId?.Trim());

        return Edit(filter, context);
    }
}
