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
/// Edits the AI Guardrail Triggered event.
/// </summary>
public sealed class AIGuardrailTriggeredEventDisplayDriver : ActivityDisplayDriver<AIGuardrailTriggeredEvent, AIGuardrailTriggeredEventViewModel>
{
    private readonly IAIProfileStore _profileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIGuardrailTriggeredEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    public AIGuardrailTriggeredEventDisplayDriver(IAIProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(AIGuardrailTriggeredEvent activity, AIGuardrailTriggeredEventViewModel model)
    {
        model.ProfileId = activity.ProfileId;
        model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Select(profile => new SelectListItem(profile.DisplayText, profile.ItemId));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AIGuardrailTriggeredEvent activity, UpdateEditorContext context)
    {
        var model = new AIGuardrailTriggeredEventViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        activity.ProfileId = model.ProfileId;

        return Edit(activity, context);
    }
}
