using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Prompting.Drivers;

/// <summary>
/// Display driver for the AI profile template prompt selection shape.
/// </summary>
public sealed class AIProfileTemplatePromptSelectionDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly PromptTemplateSelectionService _promptTemplateSelectionService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplatePromptSelectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="promptTemplateSelectionService">The prompt template selection service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplatePromptSelectionDisplayDriver(
        PromptTemplateSelectionService promptTemplateSelectionService,
        IStringLocalizer<AIProfileTemplatePromptSelectionDisplayDriver> stringLocalizer)
    {
        _promptTemplateSelectionService = promptTemplateSelectionService;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfileTemplate template, BuildEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var promptMetadata = template.GetOrCreate<PromptTemplateMetadata>();
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
        }).Location(l => l.Zone("Content", "15").Card(S["Instructions"], "4"));
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
