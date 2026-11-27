using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using CrestApps.OrchardCore.AI.Core.Workflows;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.Localization;
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

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigPromptSelectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="promptTemplateSelectionService">The prompt template selection service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AICompletionWithConfigPromptSelectionDisplayDriver(
        PromptTemplateSelectionService promptTemplateSelectionService,
        IStringLocalizer<AICompletionWithConfigPromptSelectionDisplayDriver> stringLocalizer)
    {
        _promptTemplateSelectionService = promptTemplateSelectionService;
        S = stringLocalizer;
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
        }).Location(l => l.Zone("Content", "3").Tab(S["Content"], "1").Card(S["Prompt"], "10"));
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
