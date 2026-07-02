using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class AISubjectFlowSettingsDisplayDriver : DisplayDriver<SubjectFlowSettings>
{
    private readonly IAIProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AISubjectFlowSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AISubjectFlowSettingsDisplayDriver(
        IAIProfileManager profileManager,
        IStringLocalizer<AISubjectFlowSettingsDisplayDriver> stringLocalizer)
    {
        _profileManager = profileManager;
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
            model.NoResponseTimeoutInMinutes = flowSettings.NoResponseTimeoutInMinutes;
            model.SmsResponseDelayInSeconds = flowSettings.SmsResponseDelayInSeconds;
            model.SmsOptOutKeywords = string.Join(Environment.NewLine, OmnichannelSmsComplianceHelper.NormalizeOptOutKeywords(flowSettings.SmsOptOutKeywords));

            var chatProfiles = await _profileManager.GetAsync(AIProfileType.Chat);

            model.Profiles = chatProfiles
                .Where(HasInitialPrompt)
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
                else if (!HasInitialPrompt(profile))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["The selected AI profile must have Add initial prompt enabled."]);
                }
            }

            if (model.NoResponseTimeoutInMinutes is <= 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.NoResponseTimeoutInMinutes), S["No-response timeout must be greater than zero minutes."]);
            }

            if (model.SmsResponseDelayInSeconds is < 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.SmsResponseDelayInSeconds), S["SMS response delay cannot be negative."]);
            }
        }

        flowSettings.InitialOutboundPromptPattern = null;
        flowSettings.SubjectGoal = model.SubjectGoal;
        flowSettings.ProfileId = model.ProfileId;
        flowSettings.AllowAIToUpdateContact = model.AllowAIToUpdateContact;
        flowSettings.AllowAIToUpdateSubject = model.AllowAIToUpdateSubject;
        flowSettings.NoResponseTimeoutInMinutes = model.NoResponseTimeoutInMinutes;
        flowSettings.SmsResponseDelayInSeconds = model.SmsResponseDelayInSeconds;
        flowSettings.SmsOptOutKeywords = OmnichannelSmsComplianceHelper.ParseOptOutKeywords(model.SmsOptOutKeywords).ToArray();

        return Edit(flowSettings, context);
    }

    private static bool HasInitialPrompt(AIProfile profile)
    {
        var metadata = profile.GetOrCreate<AIProfileMetadata>();

        return !string.IsNullOrWhiteSpace(metadata.InitialPrompt);
    }
}
