using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

public sealed class AICompletionTaskDisplayDriver : ActivityDisplayDriver<AICompletionTask, AICompletionTaskViewModel>
{
    private readonly INamedCatalog<AIProfile> _profilesCatalog;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    public AICompletionTaskDisplayDriver(
        INamedCatalog<AIProfile> profilesCatalog,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<AICompletionTaskDisplayDriver> stringLocalizer)
    {
        _profilesCatalog = profilesCatalog;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    protected override async ValueTask EditActivityAsync(AICompletionTask activity, AICompletionTaskViewModel model)
    {
        model.ProfileId = activity.ProfileId;
        model.PromptTemplate = activity.PromptTemplate;
        model.ResultPropertyName = activity.ResultPropertyName;

        model.Profiles = (await _profilesCatalog.GetAllAsync())
            .Select(profile => new SelectListItem(profile.DisplayText, profile.ItemId));
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionTask activity, UpdateEditorContext context)
    {
        var model = new AICompletionTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.ProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["The Profile is required."]);
        }
        else
        {
            var profile = await _profilesCatalog.FindByIdAsync(model.ProfileId);

            if (profile is null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["The Profile is invalid."]);
            }
        }

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

        activity.ProfileId = model.ProfileId;
        activity.PromptTemplate = model.PromptTemplate;
        activity.ResultPropertyName = model.ResultPropertyName?.Trim();

        return Edit(activity, context);
    }
}
