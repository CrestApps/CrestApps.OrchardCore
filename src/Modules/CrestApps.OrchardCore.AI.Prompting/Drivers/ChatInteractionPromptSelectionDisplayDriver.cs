using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Prompting.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Prompting.Drivers;

public sealed class ChatInteractionPromptSelectionDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly IAITemplateService _aiTemplateService;

    public ChatInteractionPromptSelectionDisplayDriver(IAITemplateService aiTemplateService)
    {
        _aiTemplateService = aiTemplateService;
    }

    public override async Task<IDisplayResult> EditAsync(ChatInteraction interaction, BuildEditorContext context)
    {
        var templates = await _aiTemplateService.ListAsync();
        var listableTemplates = templates.Where(t => t.Metadata.IsListable).ToList();

        if (listableTemplates.Count == 0)
        {
            return null;
        }

        return Initialize<AITemplateSelectionViewModel>("ChatInteractionPromptSelection_Edit", model =>
        {
            var promptMetadata = interaction.As<PromptTemplateMetadata>();

            model.SelectedPromptId = promptMetadata.TemplateId;
            model.PromptParameters = AIProfilePromptSelectionDisplayDriver.SerializeParameters(promptMetadata.Parameters);

            AIProfilePromptSelectionDisplayDriver.PopulateViewModel(model, listableTemplates);
        }).Location("Parameters:7#Settings;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var model = new AITemplateSelectionViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var promptMetadata = new PromptTemplateMetadata();

        if (!string.IsNullOrEmpty(model.SelectedPromptId))
        {
            promptMetadata.TemplateId = model.SelectedPromptId;

            if (!string.IsNullOrEmpty(model.PromptParameters))
            {
                var parameters = AIProfilePromptSelectionDisplayDriver.ParseAndValidateParameters(model.PromptParameters);

                if (parameters != null)
                {
                    var template = await _aiTemplateService.GetAsync(model.SelectedPromptId);
                    var invalidKeys = AIProfilePromptSelectionDisplayDriver.GetInvalidParameterKeys(parameters, template);

                    if (invalidKeys.Count > 0)
                    {
                        context.Updater.ModelState.AddModelError(
                            Prefix + '.' + nameof(model.PromptParameters),
                            $"The following parameter keys are not supported by this template: {string.Join(", ", invalidKeys)}");
                    }
                    else
                    {
                        promptMetadata.Parameters = parameters;
                    }
                }
                else
                {
                    context.Updater.ModelState.AddModelError(
                        Prefix + '.' + nameof(model.PromptParameters),
                        "The parameters must be valid JSON with string key-value pairs. Example: {\"key1\": \"value1\"}");
                }
            }
        }

        interaction.Put(promptMetadata);

        return await EditAsync(interaction, context);
    }
}
