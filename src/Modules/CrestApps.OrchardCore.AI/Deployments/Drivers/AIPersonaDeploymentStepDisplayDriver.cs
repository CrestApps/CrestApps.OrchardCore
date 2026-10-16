using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Deployments.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Deployments.Drivers;

/// <summary>
/// Display driver for the AI personas deployment step.
/// </summary>
internal sealed class AIPersonaDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, AIPersonaDeploymentStep>
{
    private readonly INamedCatalog<AIPersona> _personasCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonaDeploymentStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="personasCatalog">The catalog of saved personas.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIPersonaDeploymentStepDisplayDriver(
        INamedCatalog<AIPersona> personasCatalog,
        IStringLocalizer<AIPersonaDeploymentStepDisplayDriver> stringLocalizer)
    {
        _personasCatalog = personasCatalog;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task<IDisplayResult> DisplayAsync(AIPersonaDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIPersonaDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("AIPersonaDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content")
        );
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(AIPersonaDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<AIPersonaDeploymentStepViewModel>("AIPersonaDeploymentStep_Fields_Edit", async model =>
        {
            model.IncludeAll = step.IncludeAll;
            model.Personas = (await _personasCatalog.GetAllAsync())
                .Select(persona => new AIPersonaEntryViewModel
                {
                    ItemId = persona.ItemId,
                    DisplayText = persona.DisplayText ?? persona.Name,
                    IsSelected = step.PersonaIds?.Contains(persona.ItemId) ?? false,
                })
                .OrderBy(entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }).Location("Content");
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(AIPersonaDeploymentStep step, UpdateEditorContext context)
    {
        var model = new AIPersonaDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            p => p.IncludeAll,
            p => p.Personas);

        if (model.IncludeAll)
        {
            step.IncludeAll = true;
            step.PersonaIds = [];
        }
        else
        {
            var selected = (model.Personas ?? [])
                .Where(entry => entry.IsSelected)
                .Select(entry => entry.ItemId)
                .ToArray();

            if (selected.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Personas), S["At least one persona is required."]);
            }

            step.IncludeAll = false;
            step.PersonaIds = selected;
        }

        return Edit(step, context);
    }
}
