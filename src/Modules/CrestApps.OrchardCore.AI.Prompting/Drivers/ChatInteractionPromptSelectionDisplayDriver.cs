using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Prompting.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Prompting.Drivers;

public sealed class ChatInteractionPromptSelectionDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly PromptTemplateSelectionService _promptTemplateSelectionService;

    public ChatInteractionPromptSelectionDisplayDriver(PromptTemplateSelectionService promptTemplateSelectionService)
    {
        _promptTemplateSelectionService = promptTemplateSelectionService;
    }

    public override async Task<IDisplayResult> EditAsync(ChatInteraction interaction, BuildEditorContext context)
    {
        var promptMetadata = interaction.As<PromptTemplateMetadata>();
        var model = new AITemplateSelectionViewModel();

        await PromptTemplateSelectionEditorHelper.PopulateViewModelAsync(model, promptMetadata, _promptTemplateSelectionService);

        if (model.AvailablePrompts.Count == 0)
        {
            return null;
        }

        return Initialize<AITemplateSelectionViewModel>("PromptTemplateSelection_Edit", promptSelectionModel =>
        {
            promptSelectionModel.PromptTemplates = model.PromptTemplates;
            promptSelectionModel.AvailablePrompts = model.AvailablePrompts;
        }).Location("Parameters:9#Settings;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var model = new AITemplateSelectionViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var promptMetadata = await PromptTemplateSelectionEditorHelper.BuildMetadataAsync(
            model,
            _promptTemplateSelectionService,
            context.Updater.ModelState,
            Prefix);

        interaction.Put(promptMetadata);

        return await EditAsync(interaction, context);
    }
}
