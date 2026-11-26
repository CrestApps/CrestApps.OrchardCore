using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Prompting.Drivers;

/// <summary>
/// Display driver for the chat interaction prompt selection shape.
/// </summary>
public sealed class ChatInteractionPromptSelectionDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly PromptTemplateSelectionService _promptTemplateSelectionService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionPromptSelectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="promptTemplateSelectionService">The prompt template selection service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChatInteractionPromptSelectionDisplayDriver(
        PromptTemplateSelectionService promptTemplateSelectionService,
        IStringLocalizer<ChatInteractionPromptSelectionDisplayDriver> stringLocalizer)
    {
        _promptTemplateSelectionService = promptTemplateSelectionService;
        S = stringLocalizer;
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

        return Initialize<AITemplateSelectionViewModel>("PromptTemplateChatInteractionSelection_Edit", promptSelectionModel =>
        {
            promptSelectionModel.PromptTemplates = model.PromptTemplates;
            promptSelectionModel.AvailablePrompts = model.AvailablePrompts;
        }).Location(l => l.Zone("Parameters", "9").Tab(S["Settings"], "1").Card(S["Instructions"], "4"));
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
