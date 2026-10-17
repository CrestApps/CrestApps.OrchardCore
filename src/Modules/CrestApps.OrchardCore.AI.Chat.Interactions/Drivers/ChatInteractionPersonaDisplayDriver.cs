using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Adds the persona picker to the chat interaction settings. The chat page saves the choice through the
/// <c>personaId</c> hub setting.
/// </summary>
internal sealed class ChatInteractionPersonaDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly IAIPersonaManager _personaManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionPersonaDisplayDriver"/> class.
    /// </summary>
    /// <param name="personaManager">The persona manager, which lists the personas to pick from.</param>
    public ChatInteractionPersonaDisplayDriver(IAIPersonaManager personaManager)
    {
        _personaManager = personaManager;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        // Directly above the system instructions, which the persona's instructions come before.
        return Initialize<AIPersonaPickerViewModel>("ChatInteractionPersonaPicker_Edit", model => _personaManager.PopulatePersonaPickerAsync(model, interaction))
            .Location("Parameters:7.5#Settings;1");
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var model = new AIPersonaPickerViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        interaction.SetPersona(model.PersonaId);

        return Edit(interaction, context);
    }
}
