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
/// Display driver for the AI chat session closed event shape.
/// </summary>
public sealed class AIChatSessionClosedEventDisplayDriver : ActivityDisplayDriver<AIChatSessionClosedEvent, AIChatSessionClosedEventViewModel>
{
    private readonly IAIProfileStore _profileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionClosedEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The profile store.</param>
    public AIChatSessionClosedEventDisplayDriver(
        IAIProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    protected override async ValueTask EditActivityAsync(AIChatSessionClosedEvent activity, AIChatSessionClosedEventViewModel model)
    {
        model.ProfileId = activity.ProfileId;
        model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
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
