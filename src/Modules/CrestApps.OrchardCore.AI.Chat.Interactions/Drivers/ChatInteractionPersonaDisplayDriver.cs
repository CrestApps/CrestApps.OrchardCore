using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using Microsoft.Extensions.Localization;
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

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionPersonaDisplayDriver"/> class.
    /// </summary>
    /// <param name="personaManager">The persona manager, which lists the personas to pick from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChatInteractionPersonaDisplayDriver(
        IAIPersonaManager personaManager,
        IStringLocalizer<ChatInteractionPersonaDisplayDriver> stringLocalizer)
    {
        _personaManager = personaManager;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(ChatInteraction interaction, BuildEditorContext context)
    {
        // Directly above the system instructions, which the persona's instructions come before.
        return Initialize<AIPersonaPickerViewModel>("ChatInteractionPersonaPicker_Edit", model => _personaManager.PopulatePersonaPickerAsync(model, interaction))
            .Location(l => l.Zone("Parameters", "7.5").Tab(S["Settings"], "1").Card(S["Instructions"], "4"));
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
