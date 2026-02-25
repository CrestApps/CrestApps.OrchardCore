using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that provides profile filter control for chat analytics.
/// </summary>
public sealed class AIChatAnalyticsProfileFilterDisplayDriver : DisplayDriver<AIChatAnalyticsFilter>
{
    private readonly INamedCatalog<AIProfile> _profilesCatalog;

    public AIChatAnalyticsProfileFilterDisplayDriver(
        INamedCatalog<AIProfile> profilesCatalog)
    {
        _profilesCatalog = profilesCatalog;
    }

    public override IDisplayResult Edit(AIChatAnalyticsFilter filter, BuildEditorContext context)
    {
        return Initialize<ChatAnalyticsProfileFilterViewModel>("ChatAnalyticsProfileFilter_Edit", async model =>
        {
            model.ProfileId = filter.ProfileId;
            model.Profiles = (await _profilesCatalog.GetAsync(AIProfileType.Chat))
                .Select(p => new SelectListItem(p.DisplayText, p.ItemId));
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatAnalyticsFilter filter, UpdateEditorContext context)
    {
        var model = new ChatAnalyticsProfileFilterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.ProfileId = model.ProfileId;

        if (!string.IsNullOrEmpty(filter.ProfileId))
        {
            var profileId = filter.ProfileId;
            filter.Conditions.Add(q => q.With<AIChatSessionMetricsIndex>(i => i.ProfileId == profileId));
        }

        return Edit(filter, context);
    }
}
