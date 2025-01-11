using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.Workflows.Models;
using CrestApps.OrchardCore.OpenAI.Workflows.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.OpenAI.Workflows.Drivers;

public sealed class ChatUtilityCompletionTaskDisplayDriver : ActivityDisplayDriver<ChatUtilityCompletionTask, ChatUtilityCompletionTaskViewModel>
{
    private readonly IOpenAIChatProfileStore _chatProfileStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    public ChatUtilityCompletionTaskDisplayDriver(
        IOpenAIChatProfileStore chatProfileStore,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<ChatUtilityCompletionTaskDisplayDriver> stringLocalizer)
    {
        _chatProfileStore = chatProfileStore;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    protected override async ValueTask EditActivityAsync(ChatUtilityCompletionTask activity, ChatUtilityCompletionTaskViewModel model)
    {
        model.ProfileId = activity.ProfileId;
        model.PromptTemplate = activity.PromptTemplate;
        model.ResultPropertyName = activity.ResultPropertyName;
        model.RespondWithHtml = activity.RespondWithHtml;

        model.Profiles = (await _chatProfileStore.GetProfilesAsync(OpenAIChatProfileType.Utility))
            .Select(profile => new SelectListItem(profile.Name, profile.Id));
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatUtilityCompletionTask activity, UpdateEditorContext context)
    {
        var model = new ChatUtilityCompletionTaskViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.ProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["The Profile is required."]);
        }
        else
        {
            var profile = await _chatProfileStore.FindByIdAsync(model.ProfileId);

            if (profile == null || profile.Type != OpenAIChatProfileType.Utility)
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
        activity.RespondWithHtml = model.RespondWithHtml;

        return Edit(activity, context);
    }
}
