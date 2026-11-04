using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Reports;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// The groupings of the AI usage report's tables: metered usage, audio usage, chat completions and voice calls.
/// </summary>
public sealed class AIUsageGroupingReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (filter.ReportName != AIUsageAnalyticsReport.Name)
        {
            return null;
        }

        return Initialize<AIUsageGroupingReportFilterViewModel>("AIUsageGroupingReportFilter_Edit", model =>
        {
            model.MeteringGroupBy = filter.GetOrDefault<AIUsageReportGrouping>(AIChatReportFilters.MeteringGroupBy);
            model.AudioGroupBy = filter.GetOrDefault<AIAudioUsageReportGrouping>(AIChatReportFilters.AudioGroupBy);
            model.GroupBy = filter.GetOrDefault<AICompletionUsageGroupBy>(AIChatReportFilters.CompletionGroupBy);
            model.VoiceGroupBy = filter.GetOrDefault<AIVoiceUsageGroupBy>(AIChatReportFilters.VoiceGroupBy);
        }).Location("Content:3");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (filter.ReportName != AIUsageAnalyticsReport.Name)
        {
            return null;
        }

        var model = new AIUsageGroupingReportFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.Set(AIChatReportFilters.MeteringGroupBy, model.MeteringGroupBy.ToString());
        filter.Set(AIChatReportFilters.AudioGroupBy, model.AudioGroupBy.ToString());
        filter.Set(AIChatReportFilters.CompletionGroupBy, model.GroupBy.ToString());
        filter.Set(AIChatReportFilters.VoiceGroupBy, model.VoiceGroupBy.ToString());

        return Edit(filter, context);
    }
}
