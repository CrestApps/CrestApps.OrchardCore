using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Prompting.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Prompting.Drivers;

/// <summary>
/// Display driver for the chat interaction prompt selection shape.
/// </summary>
public sealed class ChatInteractionPromptSelectionDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly PromptTemplateSelectionService _promptTemplateSelectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionPromptSelectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="promptTemplateSelectionService">The prompt template selection service.</param>
    public ChatInteractionPromptSelectionDisplayDriver(PromptTemplateSelectionService promptTemplateSelectionService)
    {
        _promptTemplateSelectionService = promptTemplateSelectionService;
    }

    public override async Task<IDisplayResult> EditAsync(ChatInteraction interaction, BuildEditorContext context)
    {
        var promptMetadata = interaction.GetOrCreate<PromptTemplateMetadata>();
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
