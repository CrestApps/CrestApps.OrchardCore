using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class AISubjectFlowSettingsDisplayDriver : DisplayDriver<SubjectFlowSettings>
{
    private readonly IAIProfileManager _profileManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AISubjectFlowSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="liquidTemplateManager">The liquid template manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AISubjectFlowSettingsDisplayDriver(
        IAIProfileManager profileManager,
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<AISubjectFlowSettingsDisplayDriver> stringLocalizer)
    {
        _profileManager = profileManager;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(SubjectFlowSettings flowSettings, BuildEditorContext context)
    {
        return Initialize<SubjectFlowSettingsViewModel>("AISubjectFlowSettingsFields_Edit", async model =>
        {
            model.InitialOutboundPromptPattern = flowSettings.InitialOutboundPromptPattern;
            model.SubjectGoal = flowSettings.SubjectGoal;
            model.ProfileId = flowSettings.ProfileId;
            model.AllowAIToUpdateContact = !context.IsNew && flowSettings.AllowAIToUpdateContact;
            model.AllowAIToUpdateSubject = context.IsNew || flowSettings.AllowAIToUpdateSubject;

            var chatProfiles = await _profileManager.GetAsync(AIProfileType.Chat);

            model.Profiles = chatProfiles
                .OrderBy(p => p.DisplayText ?? p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => new SelectListItem(p.DisplayText ?? p.Name, p.ItemId));
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(SubjectFlowSettings flowSettings, UpdateEditorContext context)
    {
        var model = new SubjectFlowSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.InteractionType == ActivityInteractionType.Automated)
        {
            if (string.IsNullOrWhiteSpace(model.SubjectGoal))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.SubjectGoal), S["Subject goal is required for automated interactions."]);
            }

            if (string.IsNullOrWhiteSpace(model.ProfileId))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["AI profile is required for automated interactions."]);
            }
            else
            {
                var profile = await _profileManager.FindByIdAsync(model.ProfileId);

                if (profile is null || profile.Type != AIProfileType.Chat)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["The selected AI profile is invalid."]);
                }
            }

            if (string.IsNullOrWhiteSpace(model.InitialOutboundPromptPattern))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.InitialOutboundPromptPattern), S["Initial outbound prompt pattern is required for automated interactions."]);
            }
            else if (!_liquidTemplateManager.Validate(model.InitialOutboundPromptPattern, out var errors))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.InitialOutboundPromptPattern), S["The initial outbound prompt doesn't contain a valid Liquid expression. Details: {0}", string.Join(' ', errors)]);
            }
        }

        flowSettings.InitialOutboundPromptPattern = model.InitialOutboundPromptPattern;
        flowSettings.SubjectGoal = model.SubjectGoal;
        flowSettings.ProfileId = model.ProfileId;
        flowSettings.AllowAIToUpdateContact = model.AllowAIToUpdateContact;
        flowSettings.AllowAIToUpdateSubject = model.AllowAIToUpdateSubject;

        return Edit(flowSettings, context);
    }
}
