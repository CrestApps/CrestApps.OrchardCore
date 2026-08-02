using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Prompting.Drivers;
using CrestApps.OrchardCore.AI.Prompting.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Display driver that contributes the prompt template selection to the
/// <see cref="AICompletionWithConfigTask"/> workflow activity Settings tab.
/// </summary>
public sealed class AICompletionWithConfigPromptSelectionDisplayDriver : DisplayDriver<IActivity, AICompletionWithConfigTask>
{
    private readonly PromptTemplateSelectionService _promptTemplateSelectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigPromptSelectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="promptTemplateSelectionService">The prompt template selection service.</param>
    public AICompletionWithConfigPromptSelectionDisplayDriver(PromptTemplateSelectionService promptTemplateSelectionService)
    {
        _promptTemplateSelectionService = promptTemplateSelectionService;
    }

    public override async Task<IDisplayResult> EditAsync(AICompletionWithConfigTask activity, BuildEditorContext context)
    {
        var interaction = activity.Interaction;
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
        }).Location("Content:3#Settings;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        var model = new AITemplateSelectionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var promptMetadata = await PromptTemplateSelectionEditorHelper.BuildMetadataAsync(
            model,
            _promptTemplateSelectionService,
            context.Updater.ModelState,
            Prefix);

        var interaction = activity.Interaction;

        interaction.Put(promptMetadata);

        activity.Interaction = interaction;

        return await EditAsync(activity, context);
    }
}
