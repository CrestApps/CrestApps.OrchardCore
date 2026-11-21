using CrestApps.OrchardCore.Learning.ViewModels;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Reports;

/// <summary>
/// The learner filter of the Learner transcript report: a user name, or empty for everyone.
/// </summary>
internal sealed class LearningTranscriptFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (filter.ReportName != LearningTranscriptReport.Name)
        {
            return null;
        }

        return Initialize<LearningTranscriptFilterViewModel>("LearningTranscriptFilter_Edit", model =>
        {
            model.Learner = filter.GetOrDefault<string>(LearningTranscriptReport.LearnerFilterKey);
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (filter.ReportName != LearningTranscriptReport.Name)
        {
            return null;
        }

        var model = new LearningTranscriptFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.Set(LearningTranscriptReport.LearnerFilterKey, model.Learner?.Trim());

        return Edit(filter, context);
    }
}
