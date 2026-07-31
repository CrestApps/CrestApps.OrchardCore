using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelSubjectAISettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<OmnichannelSubjectPart>
{
    private readonly IAIProfileManager _profileManager;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly DefaultSpeechVoicePresenter _speechVoicePresenter;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectAISettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="deploymentManager">The AI deployment manager.</param>
    /// <param name="speechVoicePresenter">The speech voice presenter.</param>
    public OmnichannelSubjectAISettingsDisplayDriver(
        IAIProfileManager profileManager,
        IAIDeploymentManager deploymentManager,
        DefaultSpeechVoicePresenter speechVoicePresenter)
    {
        _profileManager = profileManager;
        _deploymentManager = deploymentManager;
        _speechVoicePresenter = speechVoicePresenter;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<OmnichannelSubjectAISettingsViewModel>("OmnichannelSubjectAISettings_Edit", async model =>
        {
            var settings = contentTypePartDefinition.GetSettings<OmnichannelSubjectAISettings>();
            var baseSettings = contentTypePartDefinition.GetSettings<OmnichannelSubjectPartSettings>();
            var isInbound = baseSettings.Direction == SubjectDirection.Inbound;

            // Outbound subjects resolve their interaction type and channel when an activity batch is loaded, so
            // every AI default must stay configurable for them. Inbound subjects declare both up front, which
            // lets the editor hide the settings that can never apply.
            model.CanAutomate = !isInbound || baseSettings.InteractionType == ActivityInteractionType.Automated;
            model.ShowVoiceSettings = model.CanAutomate && (!isInbound || string.Equals(baseSettings.Channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase));
            model.ShowSmsSettings = model.CanAutomate && (!isInbound || string.Equals(baseSettings.Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase));

            model.ProfileId = settings.ProfileId;
            model.SubjectGoal = settings.SubjectGoal;
            model.SpeechToTextDeploymentName = settings.SpeechToTextDeploymentName;
            model.TextToSpeechDeploymentName = settings.TextToSpeechDeploymentName;
            model.TextToSpeechVoiceId = settings.TextToSpeechVoiceId;
            model.AllowAIToUpdateContact = settings.AllowAIToUpdateContact;
            model.AllowAIToUpdateSubject = settings.AllowAIToUpdateSubject;
            model.NoResponseTimeoutInMinutes = settings.NoResponseTimeoutInMinutes;
            model.SmsResponseDelayInSeconds = settings.SmsResponseDelayInSeconds;
            model.SmsOptOutKeywords = string.Join(Environment.NewLine, OmnichannelSmsComplianceHelper.NormalizeOptOutKeywords(settings.SmsOptOutKeywords));

            var chatProfiles = await _profileManager.GetAsync(AIProfileType.Chat);

            model.Profiles = chatProfiles
                .Where(HasInitialPrompt)
                .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.OrdinalIgnoreCase)
                .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId));
            model.SpeechToTextDeployments = BuildDeploymentOptions(
                await _deploymentManager.GetByPurposeAsync(AIDeploymentPurpose.SpeechToText),
                model.SpeechToTextDeploymentName);
            model.TextToSpeechDeployments = BuildDeploymentOptions(
                await _deploymentManager.GetByPurposeAsync(AIDeploymentPurpose.TextToSpeech),
                model.TextToSpeechDeploymentName);
            model.TextToSpeechVoices = SelectVoice(
                await _speechVoicePresenter.GetVoiceMenuItemsAsync(model.TextToSpeechDeploymentName),
                model.TextToSpeechVoiceId);
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new OmnichannelSubjectAISettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        context.Builder.WithSettings(new OmnichannelSubjectAISettings
        {
            ProfileId = model.ProfileId,
            SubjectGoal = model.SubjectGoal,
            SpeechToTextDeploymentName = model.SpeechToTextDeploymentName?.Trim(),
            TextToSpeechDeploymentName = model.TextToSpeechDeploymentName?.Trim(),
            TextToSpeechVoiceId = model.TextToSpeechVoiceId?.Trim(),
            AllowAIToUpdateContact = model.AllowAIToUpdateContact,
            AllowAIToUpdateSubject = model.AllowAIToUpdateSubject,
            NoResponseTimeoutInMinutes = model.NoResponseTimeoutInMinutes,
            SmsResponseDelayInSeconds = model.SmsResponseDelayInSeconds,
            SmsOptOutKeywords = OmnichannelSmsComplianceHelper.ParseOptOutKeywords(model.SmsOptOutKeywords).ToArray(),
        });

        return Edit(contentTypePartDefinition, context);
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
