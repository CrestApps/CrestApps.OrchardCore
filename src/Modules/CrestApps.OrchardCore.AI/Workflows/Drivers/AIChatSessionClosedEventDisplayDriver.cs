using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

public sealed class AIChatSessionClosedEventDisplayDriver : ActivityDisplayDriver<AIChatSessionClosedEvent, AIChatSessionClosedEventViewModel>
{
    private readonly INamedCatalog<AIProfile> _profilesCatalog;

    public AIChatSessionClosedEventDisplayDriver(
        INamedCatalog<AIProfile> profilesCatalog)
    {
        _profilesCatalog = profilesCatalog;
    }

    protected override async ValueTask EditActivityAsync(AIChatSessionClosedEvent activity, AIChatSessionClosedEventViewModel model)
    {
        model.ProfileId = activity.ProfileId;
        model.Profiles = (await _profilesCatalog.GetAllAsync())
            .Where(p => p.Type == AIProfileType.Chat)
            .Select(p => new SelectListItem(p.DisplayText, p.ItemId));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatSessionClosedEvent activity, UpdateEditorContext context)
    {
        var model = new AIChatSessionClosedEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.ProfileId = model.ProfileId;

        return Edit(activity, context);
    }
}
