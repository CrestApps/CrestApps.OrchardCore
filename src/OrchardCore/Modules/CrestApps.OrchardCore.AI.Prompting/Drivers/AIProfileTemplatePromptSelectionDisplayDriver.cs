using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Prompting.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Prompting.Drivers;

public sealed class AIProfileTemplatePromptSelectionDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly PromptTemplateSelectionService _promptTemplateSelectionService;

    public AIProfileTemplatePromptSelectionDisplayDriver(PromptTemplateSelectionService promptTemplateSelectionService)
    {
        _promptTemplateSelectionService = promptTemplateSelectionService;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfileTemplate template, BuildEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var promptMetadata = template.As<PromptTemplateMetadata>();
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
        }).Location("Content:15%Instructions;4");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new AITemplateSelectionViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var promptMetadata = await PromptTemplateSelectionEditorHelper.BuildMetadataAsync(
            model,
            _promptTemplateSelectionService,
            context.Updater.ModelState,
            Prefix);

        template.Put(promptMetadata);

        return await EditAsync(template, context);
    }
}
