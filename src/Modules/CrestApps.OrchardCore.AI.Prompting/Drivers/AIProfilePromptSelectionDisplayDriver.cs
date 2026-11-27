using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Prompting.Drivers;

/// <summary>
/// Display driver for the AI profile prompt selection shape.
/// </summary>
public sealed class AIProfilePromptSelectionDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly PromptTemplateSelectionService _promptTemplateSelectionService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfilePromptSelectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="promptTemplateSelectionService">The prompt template selection service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfilePromptSelectionDisplayDriver(
        PromptTemplateSelectionService promptTemplateSelectionService,
        IStringLocalizer<AIProfilePromptSelectionDisplayDriver> stringLocalizer)
    {
        _promptTemplateSelectionService = promptTemplateSelectionService;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfile profile, BuildEditorContext context)
    {
        var promptMetadata = profile.GetOrCreate<PromptTemplateMetadata>();
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

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new AITemplateSelectionViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var promptMetadata = await PromptTemplateSelectionEditorHelper.BuildMetadataAsync(
            model,
            _promptTemplateSelectionService,
            context.Updater.ModelState,
            Prefix);

        profile.Put(promptMetadata);

        return await EditAsync(profile, context);
    }
}
