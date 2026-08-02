using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Primary display driver for the <see cref="AICompletionWithConfigTask"/> workflow activity.
/// Renders the prompt template and result property name in the Settings tab.
/// </summary>
public sealed class AICompletionWithConfigTaskDisplayDriver : ActivityDisplayDriver<AICompletionWithConfigTask, AICompletionWithConfigTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager for template validation.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public AICompletionWithConfigTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<AICompletionWithConfigTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AICompletionWithConfigTask activity, BuildEditorContext context)
    {
        return Initialize<AICompletionWithConfigTaskViewModel>(ActivityName + "_Fields_Edit", model =>
        {
            model.PromptTemplate = activity.PromptTemplate;
            model.ResultPropertyName = activity.ResultPropertyName;
        }).Location("Content:1#Settings;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        var model = new AICompletionWithConfigTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.PromptTemplate))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PromptTemplate), S["The Prompt template is required."]);
        }
        else if (!_liquidTemplateManager.Validate(model.PromptTemplate, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PromptTemplate), S["The Prompt template is invalid."]);
        }

        if (string.IsNullOrWhiteSpace(model.ResultPropertyName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ResultPropertyName), S["The Property name is required."]);
        }

        activity.PromptTemplate = model.PromptTemplate;
        activity.ResultPropertyName = model.ResultPropertyName?.Trim();

        return Edit(activity, context);
    }
}
