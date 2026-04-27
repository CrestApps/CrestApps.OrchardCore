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
/// Display driver for the AI chat session all fields extracted event shape.
/// </summary>
public sealed class AIChatSessionAllFieldsExtractedEventDisplayDriver : ActivityDisplayDriver<AIChatSessionAllFieldsExtractedEvent, AIChatSessionAllFieldsExtractedEventViewModel>
{
    private readonly IAIProfileStore _profileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionAllFieldsExtractedEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The profile store.</param>
    public AIChatSessionAllFieldsExtractedEventDisplayDriver(
        IAIProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    protected override async ValueTask EditActivityAsync(AIChatSessionAllFieldsExtractedEvent activity, AIChatSessionAllFieldsExtractedEventViewModel model)
    {
        model.ProfileId = activity.ProfileId;
        model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
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
