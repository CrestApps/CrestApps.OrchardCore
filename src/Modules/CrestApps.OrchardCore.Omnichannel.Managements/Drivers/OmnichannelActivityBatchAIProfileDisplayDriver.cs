using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelActivityBatchAIProfileDisplayDriver : DisplayDriver<OmnichannelActivityBatch>
{
    private readonly IAIProfileManager _profileManager;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly DefaultSpeechVoicePresenter _speechVoicePresenter;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;

    internal readonly IStringLocalizer S;

    public OmnichannelActivityBatchAIProfileDisplayDriver(
        IAIProfileManager profileManager,
        IAIDeploymentManager deploymentManager,
        DefaultSpeechVoicePresenter speechVoicePresenter,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IStringLocalizer<OmnichannelActivityBatchAIProfileDisplayDriver> stringLocalizer)
    {
        _profileManager = profileManager;
        _deploymentManager = deploymentManager;
        _speechVoicePresenter = speechVoicePresenter;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(OmnichannelActivityBatch batch, BuildEditorContext context)
    {
        if (!string.Equals(batch.Source, ActivitySources.Automatic, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Initialize<OmnichannelActivityBatchAIProfileViewModel>("OmnichannelActivityBatchAIProfileFields_Edit", async model =>
        {
            model.Source = batch.Source;
            model.AIProfileId = batch.AIProfileId;
            model.SpeechToTextDeploymentName = batch.SpeechToTextDeploymentName;
            model.TextToSpeechDeploymentName = batch.TextToSpeechDeploymentName;
            model.TextToSpeechVoiceId = batch.TextToSpeechVoiceId;

            var selectedProfileId = model.AIProfileId;
            SubjectFlowSettings flowSettings = null;

            if (!string.IsNullOrWhiteSpace(batch.SubjectContentType))
            {
                flowSettings = await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(batch.SubjectContentType);

                if (string.IsNullOrWhiteSpace(selectedProfileId))
                {
                    selectedProfileId = flowSettings?.ProfileId;
                }
            }

            model.IsPhoneChannel = string.Equals(
                flowSettings?.Channel,
                OmnichannelConstants.Channels.Phone,
                StringComparison.OrdinalIgnoreCase);
            model.AIProfiles = await GetAIProfileOptionsAsync(selectedProfileId);
            model.SpeechToTextDeployments = BuildDeploymentOptions(
                await _deploymentManager.GetByPurposeAsync(AIDeploymentPurpose.SpeechToText),
                model.SpeechToTextDeploymentName);
            model.TextToSpeechDeployments = BuildDeploymentOptions(
                await _deploymentManager.GetByPurposeAsync(AIDeploymentPurpose.TextToSpeech),
                model.TextToSpeechDeploymentName);

            var effectiveTextToSpeechDeploymentName = string.IsNullOrWhiteSpace(model.TextToSpeechDeploymentName)
                ? flowSettings?.TextToSpeechDeploymentName
                : model.TextToSpeechDeploymentName;
            var effectiveVoiceId = string.IsNullOrWhiteSpace(model.TextToSpeechVoiceId)
                ? flowSettings?.TextToSpeechVoiceId
                : model.TextToSpeechVoiceId;

            model.TextToSpeechVoices = SelectVoice(
                await _speechVoicePresenter.GetVoiceMenuItemsAsync(effectiveTextToSpeechDeploymentName),
                effectiveVoiceId);
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelActivityBatch batch, UpdateEditorContext context)
    {
        if (!string.Equals(batch.Source, ActivitySources.Automatic, StringComparison.OrdinalIgnoreCase))
        {
            batch.AIProfileId = null;
            batch.SpeechToTextDeploymentName = null;
            batch.TextToSpeechDeploymentName = null;
            batch.TextToSpeechVoiceId = null;

            return null;
        }

        var model = new OmnichannelActivityBatchAIProfileViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var flowSettings = !string.IsNullOrWhiteSpace(batch.SubjectContentType)
            ? await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(batch.SubjectContentType)
            : null;
        var selectedProfileId = string.IsNullOrWhiteSpace(model.AIProfileId)
            ? flowSettings?.ProfileId
            : model.AIProfileId.Trim();

        if (string.IsNullOrWhiteSpace(selectedProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileId), S["AI profile is required for automatic inventory loads."]);
        }
        else
        {
            var profile = await _profileManager.FindByIdAsync(selectedProfileId);

            if (profile is null || profile.Type != AIProfileType.Chat)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileId), S["The selected AI profile is invalid."]);
            }
            else if (!HasInitialPrompt(profile))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AIProfileId), S["The selected AI profile must have Add initial prompt enabled."]);
            }
        }

        batch.AIProfileId = model.AIProfileId?.Trim();
        batch.SpeechToTextDeploymentName = model.SpeechToTextDeploymentName?.Trim();
        batch.TextToSpeechDeploymentName = model.TextToSpeechDeploymentName?.Trim();
        batch.TextToSpeechVoiceId = model.TextToSpeechVoiceId?.Trim();

        return Edit(batch, context);
    }

    private async Task<IEnumerable<SelectListItem>> GetAIProfileOptionsAsync(string selectedProfileId)
    {
        var chatProfiles = await _profileManager.GetAsync(AIProfileType.Chat);

        return chatProfiles
            .Where(HasInitialPrompt)
            .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId)
            {
                Selected = string.Equals(profile.ItemId, selectedProfileId, StringComparison.OrdinalIgnoreCase),
            });
    }

    private static bool HasInitialPrompt(AIProfile profile)
    {
        var metadata = profile.GetOrCreate<AIProfileMetadata>();

        return !string.IsNullOrWhiteSpace(metadata.InitialPrompt);
    }

    private static IEnumerable<SelectListItem> BuildDeploymentOptions(
        IEnumerable<AIDeployment> deployments,
        string selectedName)
    {
        return deployments
            .OrderBy(deployment => deployment.ConnectionName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
            .Select(deployment => new SelectListItem(deployment.Name, deployment.Name)
            {
                Selected = string.Equals(deployment.Name, selectedName, StringComparison.OrdinalIgnoreCase),
            });
    }

    private static IEnumerable<SelectListItem> SelectVoice(
        IEnumerable<SelectListItem> voices,
        string selectedVoiceId)
    {
        foreach (var voice in voices)
        {
            voice.Selected = string.Equals(voice.Value, selectedVoiceId, StringComparison.OrdinalIgnoreCase);
        }

        return voices;
    }
}
