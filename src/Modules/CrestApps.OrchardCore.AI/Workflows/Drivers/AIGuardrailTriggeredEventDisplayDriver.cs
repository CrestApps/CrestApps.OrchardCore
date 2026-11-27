using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
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

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIGuardrailTriggeredEventDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIGuardrailTriggeredEventDisplayDriver(
        IAIProfileStore profileStore,
        IStringLocalizer<AIGuardrailTriggeredEventDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(AIGuardrailTriggeredEvent activity, AIGuardrailTriggeredEventViewModel model)
    {
        model.ProfileId = activity.ProfileId;
        model.Profiles = (await _profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Select(profile => new SelectListItem(profile.DisplayText, profile.ItemId));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AIGuardrailTriggeredEvent activity, BuildEditorContext context)
    {
        return Initialize<AIGuardrailTriggeredEventViewModel>(ActivityName + "_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Profile"], "10"));
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
