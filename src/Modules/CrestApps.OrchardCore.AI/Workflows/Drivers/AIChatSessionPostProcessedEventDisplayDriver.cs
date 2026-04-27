using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Display driver for the AI chat session post processed event shape.
/// </summary>
public sealed class AIChatSessionPostProcessedEventDisplayDriver : ActivityDisplayDriver<AIChatSessionPostProcessedEvent, AIChatSessionPostProcessedEventViewModel>
{
    private readonly IAIProfileStore _profileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionPostProcessedEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The profile store.</param>
    public AIChatSessionPostProcessedEventDisplayDriver(
        IAIProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    protected override async ValueTask EditActivityAsync(AIChatSessionPostProcessedEvent activity, AIChatSessionPostProcessedEventViewModel model)
    {
        model.ProfileId = activity.ProfileId;
        model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Select(p => new SelectListItem(p.DisplayText, p.ItemId));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatSessionPostProcessedEvent activity, UpdateEditorContext context)
    {
        var model = new AIChatSessionPostProcessedEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.ProfileId = model.ProfileId;

        return Edit(activity, context);
    }
}
