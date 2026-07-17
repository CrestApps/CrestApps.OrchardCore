using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that provides date range filter controls for chat analytics.
/// </summary>
public sealed class AIChatAnalyticsDateRangeFilterDisplayDriver : DisplayDriver<AIChatAnalyticsFilter>
{
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatAnalyticsDateRangeFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="localClock">The local clock for timezone conversions.</param>
    public AIChatAnalyticsDateRangeFilterDisplayDriver(ILocalClock localClock)
    {
        _localClock = localClock;
    }

    public override IDisplayResult Edit(AIChatAnalyticsFilter filter, BuildEditorContext context)
    {
        return Initialize<ChatAnalyticsFilterViewModel>("ChatAnalyticsDateRangeFilter_Edit", model =>
        {
            model.StartDate = filter.StartDate;
            model.EndDate = filter.EndDate;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatAnalyticsFilter filter, UpdateEditorContext context)
    {
        var model = new ChatAnalyticsFilterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Store the original local dates for form re-display and export.
        filter.StartDate = model.StartDate;
        filter.EndDate = model.EndDate;

        // Convert local dates to UTC before applying to the filter.
        if (model.StartDate.HasValue)
        {
            filter.StartDateUtc = await _localClock.ConvertToUtcAsync(model.StartDate.Value);
        }

        if (model.EndDate.HasValue)
        {
            filter.EndDateUtc = await _localClock.ConvertToUtcAsync(model.EndDate.Value);
        }

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
