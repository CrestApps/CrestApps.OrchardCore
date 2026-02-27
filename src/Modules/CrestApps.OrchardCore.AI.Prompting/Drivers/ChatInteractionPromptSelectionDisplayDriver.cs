using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Prompting.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

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
            model.SelectedPromptId = interaction.Properties?["PromptTemplateId"]?.ToString();
            model.PromptParameters = interaction.Properties?["PromptParameters"]?.ToString();

            AIProfilePromptSelectionDisplayDriver.PopulateViewModel(model, listableTemplates);
        }).Location("Parameters:7#Settings;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var model = new AITemplateSelectionViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        return await EditAsync(interaction, context);
    }
}
