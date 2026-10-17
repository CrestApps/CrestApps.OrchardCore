using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for AI personas: the admin list entry and the persona editor.
/// </summary>
internal sealed class AIPersonaDisplayDriver : DisplayDriver<AIPersona>
{
    private readonly INamedCatalog<AIPersona> _personasCatalog;
    private readonly IAIDeploymentManager _deploymentManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonaDisplayDriver"/> class.
    /// </summary>
    /// <param name="personasCatalog">The catalog of saved personas, which tells a saved persona from a persona file.</param>
    /// <param name="deploymentManager">The manager that lists the deployments a voice can be chosen from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIPersonaDisplayDriver(
        INamedCatalog<AIPersona> personasCatalog,
        IAIDeploymentManager deploymentManager,
        IStringLocalizer<AIPersonaDisplayDriver> stringLocalizer)
    {
        _personasCatalog = personasCatalog;
        _deploymentManager = deploymentManager;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> DisplayAsync(AIPersona persona, BuildDisplayContext context)
    {
        var isFromFile = await IsFromFileAsync(persona);

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = persona.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            // A persona that comes from a file cannot be deleted, so it cannot be selected for a bulk delete either.
            View("AIPersona_Checkbox_SummaryAdmin", persona)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1")
                .RenderWhen(() => Task.FromResult(!isFromFile)),
            View("AIPersona_Fields_SummaryAdmin", persona)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AIPersona_Description_SummaryAdmin", persona)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("AIPersona_Tags_SummaryAdmin", new AIPersonaSummaryViewModel(persona, isFromFile))
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("AIPersona_Buttons_SummaryAdmin", new AIPersonaSummaryViewModel(persona, isFromFile)).Location("Actions:5")
        );
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(AIPersona persona, BuildEditorContext context)
    {
        return Initialize<AIPersonaViewModel>("AIPersonaFields_Edit", async model =>
        {
            model.Name = persona.Name;
            model.DisplayText = persona.DisplayText;
            model.Description = persona.Description;
            model.Instructions = persona.Instructions;
            model.AssistantName = persona.AssistantName;
            model.Language = persona.Language;
            model.VoiceDeploymentName = persona.VoiceDeploymentName;
            model.VoiceName = persona.VoiceName;
            model.AvatarUrl = persona.AvatarUrl;
            model.IsNew = context.IsNew;
            model.IsFromFile = !context.IsNew && await IsFromFileAsync(persona);

            // A voice name only means something to the provider it came from, so the voice is picked from a
            // deployment and that deployment is remembered with it.
            var realtimeDeployments = (await _deploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.Realtime))
                .Where(deployment => !string.IsNullOrWhiteSpace(deployment.Name))
                .OrderBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var speechDeployments = (await _deploymentManager.GetAllBySlotAsync(AIDeploymentSlotNames.TextToSpeech))
                .Where(deployment => !string.IsNullOrWhiteSpace(deployment.Name))
                .OrderBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase);

            var realtimeGroup = new SelectListGroup { Name = S["Realtime (speech-to-speech)"] };
            var speechGroup = new SelectListGroup { Name = S["Text-to-speech"] };

            model.VoiceDeployments = realtimeDeployments
                .Select(deployment => new SelectListItem(deployment.Name, deployment.Name) { Group = realtimeGroup })
                .Concat(speechDeployments.Select(deployment => new SelectListItem(deployment.Name, deployment.Name) { Group = speechGroup }))
                .ToList();

            model.RealtimeDeploymentNames = realtimeDeployments
                .Select(deployment => deployment.Name)
                .ToArray();
        }).Location("Content:1");
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(AIPersona persona, UpdateEditorContext context)
    {
        var model = new AIPersonaViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // The technical name is what profiles and interactions link to, so only a new persona sets it.
        if (context.IsNew)
        {
            var name = Normalize(model.Name);

            if (name is null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The technical name is required."]);
            }

            persona.Name = name;
        }

        if (string.IsNullOrWhiteSpace(model.Instructions))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Instructions), S["The persona instructions are required."]);
        }

        var voiceName = Normalize(model.VoiceName);

        persona.DisplayText = Normalize(model.DisplayText) ?? persona.Name;
        persona.Description = Normalize(model.Description);
        persona.Instructions = model.Instructions?.Trim();
        persona.AssistantName = Normalize(model.AssistantName);
        persona.Language = Normalize(model.Language);
        persona.VoiceName = voiceName;

        // The deployment only qualifies a voice; without one there is nothing to qualify.
        persona.VoiceDeploymentName = voiceName is null
            ? null
            : Normalize(model.VoiceDeploymentName);
        persona.AvatarUrl = Normalize(model.AvatarUrl);

        return Edit(persona, context);
    }

    private async Task<bool> IsFromFileAsync(AIPersona persona)
    {
        return string.IsNullOrEmpty(persona.ItemId) ||
            await _personasCatalog.FindByIdAsync(persona.ItemId) is null;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
