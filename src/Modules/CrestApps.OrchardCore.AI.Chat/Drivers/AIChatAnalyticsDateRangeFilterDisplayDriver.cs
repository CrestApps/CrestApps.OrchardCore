using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Indexes;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that provides date range filter controls for chat analytics.
/// </summary>
public sealed class AIChatAnalyticsDateRangeFilterDisplayDriver : DisplayDriver<AIChatAnalyticsFilter>
{
    public override IDisplayResult Edit(AIChatAnalyticsFilter filter, BuildEditorContext context)
    {
        return Initialize<ChatAnalyticsFilterViewModel>("ChatAnalyticsDateRangeFilter_Edit", model =>
        {
            model.StartDate = filter.StartDateUtc;
            model.EndDate = filter.EndDateUtc;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatAnalyticsFilter filter, UpdateEditorContext context)
    {
        var model = new ChatAnalyticsFilterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.StartDateUtc = model.StartDate;
        filter.EndDateUtc = model.EndDate;

        // Add date range conditions to the query.
        if (filter.StartDateUtc.HasValue)
        {
            var start = filter.StartDateUtc.Value;
            filter.Conditions.Add(q => q.With<AIChatSessionMetricsIndex>(i => i.SessionStartedUtc >= start));
        }

        if (filter.EndDateUtc.HasValue)
        {
            var end = filter.EndDateUtc.Value;
            filter.Conditions.Add(q => q.With<AIChatSessionMetricsIndex>(i => i.SessionStartedUtc <= end));
        }

        return Edit(filter, context);
    }
}
