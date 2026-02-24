using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

internal sealed class AIProfileAnalyticsDisplayDriver : DisplayDriver<AIProfile>
{
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileAnalyticsViewModel>("AIProfileAnalytics_Edit", model =>
        {
            var metadata = profile.As<AIProfileAnalyticsMetadata>();
            model.EnableSessionMetrics = metadata.EnableSessionMetrics;
        }).Location("Content:5#Analytics:10")
        .RenderWhen(() => Task.FromResult(profile.Type == AIProfileType.Chat));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        if (profile.Type != AIProfileType.Chat)
        {
            return Edit(profile, context);
        }

        var model = new EditAIProfileAnalyticsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = profile.As<AIProfileAnalyticsMetadata>();
        metadata.EnableSessionMetrics = model.EnableSessionMetrics;
        profile.Put(metadata);

        return Edit(profile, context);
    }
}
