using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

public sealed class AIChatSessionAllFieldsExtractedEventDisplayDriver : ActivityDisplayDriver<AIChatSessionAllFieldsExtractedEvent, AIChatSessionAllFieldsExtractedEventViewModel>
{
    private readonly INamedCatalog<AIProfile> _profilesCatalog;

    public AIChatSessionAllFieldsExtractedEventDisplayDriver(
        INamedCatalog<AIProfile> profilesCatalog)
    {
        _profilesCatalog = profilesCatalog;
    }

    protected override async ValueTask EditActivityAsync(AIChatSessionAllFieldsExtractedEvent activity, AIChatSessionAllFieldsExtractedEventViewModel model)
    {
        model.ProfileId = activity.ProfileId;
        model.Profiles = (await _profilesCatalog.GetAsync(AIProfileType.Chat))
            .Select(p => new SelectListItem(p.DisplayText, p.ItemId));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatSessionAllFieldsExtractedEvent activity, UpdateEditorContext context)
    {
        var model = new AIChatSessionAllFieldsExtractedEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.ProfileId = model.ProfileId;

        return Edit(activity, context);
    }
}
