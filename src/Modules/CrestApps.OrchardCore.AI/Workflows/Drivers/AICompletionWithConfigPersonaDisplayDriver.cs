using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Adds the persona picker to the "AI completion with configuration" workflow task.
/// </summary>
public sealed class AICompletionWithConfigPersonaDisplayDriver : DisplayDriver<IActivity, AICompletionWithConfigTask>
{
    private readonly IAIPersonaManager _personaManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigPersonaDisplayDriver"/> class.
    /// </summary>
    /// <param name="personaManager">The persona manager, which lists the personas to pick from.</param>
    public AICompletionWithConfigPersonaDisplayDriver(IAIPersonaManager personaManager)
    {
        _personaManager = personaManager;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(AICompletionWithConfigTask activity, BuildEditorContext context)
    {
        return Initialize<AIPersonaPickerViewModel>("AIPersonaPicker_Edit", model => _personaManager.PopulatePersonaPickerAsync(model, activity.Interaction))
            .Location("Content:2.5#Content;1");
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        var model = new AIPersonaPickerViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // The task stores its interaction as a property, so it is read, changed and written back.
        var interaction = activity.Interaction;

        interaction.SetPersona(model.PersonaId);

        activity.Interaction = interaction;

        return Edit(activity, context);
    }
}
