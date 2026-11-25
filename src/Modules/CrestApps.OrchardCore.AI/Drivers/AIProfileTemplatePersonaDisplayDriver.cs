using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Adds the persona picker to Profile templates. Profiles created from the template start linked to the persona.
/// </summary>
internal sealed class AIProfileTemplatePersonaDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly IAIPersonaManager _personaManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplatePersonaDisplayDriver"/> class.
    /// </summary>
    /// <param name="personaManager">The persona manager, which lists the personas to pick from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplatePersonaDisplayDriver(
        IAIPersonaManager personaManager,
        IStringLocalizer<AIProfileTemplatePersonaDisplayDriver> stringLocalizer)
    {
        _personaManager = personaManager;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        return Initialize<AIPersonaPickerViewModel>("AIPersonaPicker_Edit", model => _personaManager.PopulatePersonaPickerAsync(model, template))
            .Location(l => l.Zone("Content", "3").Card(S["Instructions"], "4"))
            .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new AIPersonaPickerViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        template.SetPersona(model.PersonaId);

        return Edit(template, context);
    }
}
