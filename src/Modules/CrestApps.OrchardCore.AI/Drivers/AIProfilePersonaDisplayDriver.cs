using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Adds the persona picker to the AI profile editor.
/// </summary>
internal sealed class AIProfilePersonaDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIPersonaManager _personaManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfilePersonaDisplayDriver"/> class.
    /// </summary>
    /// <param name="personaManager">The persona manager, which lists the personas to pick from.</param>
    public AIProfilePersonaDisplayDriver(IAIPersonaManager personaManager)
    {
        _personaManager = personaManager;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        // Above the system instructions: the persona says who the assistant is before they say what it does.
        return Initialize<AIPersonaPickerViewModel>("AIPersonaPicker_Edit", model => _personaManager.PopulatePersonaPickerAsync(model, profile))
            .Location("Content:3%Instructions;4");
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AIPersonaPickerViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.SetPersona(model.PersonaId);

        return Edit(profile, context);
    }
}
